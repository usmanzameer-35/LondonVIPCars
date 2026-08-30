using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Invoices;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Payments;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LondonVIP.Tests.Infrastructure;
using Xunit;

namespace LondonVIP.Tests;

public class PaymentEndpointTests
{
    [Fact]
    public async Task PostPayment_CreatesPaymentRecord()
    {
        await WithAppAndCustomerAsync(async (_, client, customer) =>
        {
            var request = new PaymentCreateDto
            {
                PaymentReference = "PAY-001",
                CustomerId = customer.Id,
                PaymentMethod = "BankTransfer",
                Amount = 1000
            };

            using var response = await client.PostAsJsonAsync("/api/payments", request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            
            var payment = await response.Content.ReadFromJsonAsync<PaymentDetailDto>();
            Assert.NotNull(payment);
            Assert.Equal("PAY-001", payment.PaymentReference);
            Assert.Equal(customer.Id, payment.CustomerId);
            Assert.Equal(1000m, payment.Amount);
            Assert.Equal("BankTransfer", payment.PaymentMethod);
        });
    }

    [Fact]
    public async Task AllocatePayment_UpdatesInvoiceStatus()
    {
        await WithAppAndCustomerAsync(async (_, client, customer) =>
        {
            // Create and issue invoice
            var invoiceRequest = new InvoiceCreateDto
            {
                CustomerId = customer.Id,
                Lines = new()
                {
                    new InvoiceLineCreateDto
                    {
                        Description = "Service",
                        Quantity = 1,
                        UnitPrice = 500,
                        TaxRate = 0
                    }
                }
            };

            using var invoiceResponse = await client.PostAsJsonAsync("/api/invoices", invoiceRequest);
            var invoice = await invoiceResponse.Content.ReadFromJsonAsync<InvoiceDetailDto>();

            await client.PostAsync($"/api/invoices/{invoice!.Id}/issue", null);

            // Create payment
            var paymentRequest = new PaymentCreateDto
            {
                PaymentReference = "PAY-001",
                CustomerId = customer.Id,
                PaymentMethod = "BankTransfer",
                Amount = 500
            };

            using var paymentResponse = await client.PostAsJsonAsync("/api/payments", paymentRequest);
            var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentDetailDto>();

            // Allocate full payment
            var allocationRequest = new PaymentAllocationCreateDto
            {
                InvoiceId = invoice.Id,
                Amount = 500
            };

            using var allocateResponse = await client.PostAsJsonAsync(
                $"/api/payments/{payment!.Id}/allocate", allocationRequest);
            Assert.Equal(HttpStatusCode.Created, allocateResponse.StatusCode);

            // Check invoice status
            using var invoiceCheckResponse = await client.GetAsync($"/api/invoices/{invoice.Id}");
            var updatedInvoice = await invoiceCheckResponse.Content.ReadFromJsonAsync<InvoiceDetailDto>();
            Assert.Equal("Paid", updatedInvoice!.Status);
        });
    }

    private static async Task WithAppAndCustomerAsync(Func<WebApplication, HttpClient, Customer, Task> test)
    {
        await using var host = await TestApiHost.StartAsync();
        var app = host.App;
        var customer = new Customer { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, FirstName = "Payment", LastName = "Test", Email = $"{Guid.NewGuid():N}@example.test", Phone = "000", CreatedAt = DateTimeOffset.UtcNow, IsActive = true };
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
        }
        try
        {
            await test(app, host.Client, customer);
        }
        finally
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
            await db.PaymentAllocations.Where(pa => pa.Invoice.CustomerId == customer.Id).ExecuteDeleteAsync();
            await db.Payments.Where(p => p.CustomerId == customer.Id).ExecuteDeleteAsync();
            await db.InvoiceLines.Where(il => il.Invoice.CustomerId == customer.Id).ExecuteDeleteAsync();
            await db.Invoices.Where(i => i.CustomerId == customer.Id).ExecuteDeleteAsync();
            await db.Customers.Where(item => item.Id == customer.Id).ExecuteDeleteAsync();
        }
    }
}

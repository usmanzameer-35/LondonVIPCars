using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Invoices;
using LondonVIP.Shared.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LondonVIP.Tests.Infrastructure;
using Xunit;

namespace LondonVIP.Tests;

public class InvoiceEndpointTests
{
    [Fact]
    public async Task PostInvoice_CreatesDraftInvoiceWithValidation()
    {
        await WithAppAndCustomerAsync(async (_, client, customer) =>
        {
            var request = new InvoiceCreateDto
            {
                CustomerId = customer.Id,
                Lines = new()
                {
                    new InvoiceLineCreateDto
                    {
                        Description = "Airport Transfer",
                        Quantity = 1,
                        UnitPrice = 75,
                        TaxRate = 20
                    }
                }
            };

            using var response = await client.PostAsJsonAsync("/api/invoices", request);
            if (response.StatusCode != HttpStatusCode.Created)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Expected 201 Created but got {response.StatusCode}: {errorContent}");
            }
            
            var invoice = await response.Content.ReadFromJsonAsync<InvoiceDetailDto>();
            Assert.NotNull(invoice);
            Assert.StartsWith("LVC-", invoice.InvoiceNumber);
            Assert.Equal(customer.Id, invoice.CustomerId);
            Assert.Equal("Draft", invoice.Status);
            Assert.Equal(75m, invoice.Subtotal);
            Assert.Equal(15m, invoice.TaxAmount);
            Assert.Equal(90m, invoice.TotalAmount);
            Assert.Equal(1, invoice.Lines.Count);
        });
    }

    [Fact]
    public async Task PostInvoice_RequiresCustomerOrAccount()
    {
        await WithAppAndCustomerAsync(async (_, client, _) =>
        {
            var request = new InvoiceCreateDto
            {
                Lines = new()
                {
                    new InvoiceLineCreateDto
                    {
                        Description = "Service",
                        Quantity = 1,
                        UnitPrice = 100,
                        TaxRate = 0
                    }
                }
            };

            using var response = await client.PostAsJsonAsync("/api/invoices", request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        });
    }

    private static async Task WithAppAndCustomerAsync(Func<WebApplication, HttpClient, Customer, Task> test)
    {
        await using var host = await TestApiHost.StartAsync();
        var app = host.App;
        var customer = new Customer { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, FirstName = "Invoice", LastName = "Test", Email = $"{Guid.NewGuid():N}@example.test", Phone = "000", CreatedAt = DateTimeOffset.UtcNow, IsActive = true };
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

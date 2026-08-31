using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Crm;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Security;
using LondonVIP.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public sealed class CrmEndpointTests
{
    [Fact]
    public async Task LeadLifecyclePipelineTimelineAndAuditAreOperational()
    {
        await using var host=await TestApiHost.StartAsync();
        var leadRequest=NewLead("website@example.test");
        using var created=await host.Client.PostAsJsonAsync("/api/crm/leads",leadRequest);
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);var lead=await created.Content.ReadFromJsonAsync<CrmLeadDto>();Assert.NotNull(lead);Assert.StartsWith("LEAD-",lead.Reference);
        var found=await host.Client.GetFromJsonAsync<List<CrmLeadDto>>("/api/crm/leads?q=website%40example.test");Assert.Contains(found!,x=>x.Id==lead.Id);
        var updatedRequest=leadRequest with{Status=CrmLeadStatus.Qualified,Score=80,Probability=70};using var updated=await host.Client.PutAsJsonAsync($"/api/crm/leads/{lead.Id}",updatedRequest);Assert.Equal(HttpStatusCode.OK,updated.StatusCode);
        using var converted=await host.Client.PostAsync($"/api/crm/leads/{lead.Id}/convert",null);Assert.Equal(HttpStatusCode.OK,converted.StatusCode);
        var convertedLead=await host.Client.GetFromJsonAsync<CrmLeadDto>($"/api/crm/leads/{lead.Id}");Assert.Equal(CrmLeadStatus.Converted,convertedLead!.Status);Assert.NotNull(convertedLead.CustomerId);

        using var stageResponse=await host.Client.PostAsJsonAsync("/api/crm/stages",new PipelineStageRequest("Qualified",1,60,false,false,true));Assert.Equal(HttpStatusCode.Created,stageResponse.StatusCode);var stage=await stageResponse.Content.ReadFromJsonAsync<CrmPipelineStage>();Assert.NotNull(stage);
        using var opportunityResponse=await host.Client.PostAsJsonAsync("/api/crm/opportunities",new OpportunityRequest("Airport travel account",stage.Id,lead.Id,convertedLead.CustomerId,null,1000,60,DateTimeOffset.UtcNow.AddDays(14),null,null));Assert.Equal(HttpStatusCode.Created,opportunityResponse.StatusCode);
        var pipeline=await host.Client.GetFromJsonAsync<List<OpportunityDto>>("/api/crm/pipeline");var opportunity=Assert.Single(pipeline!);Assert.Equal(600m,opportunity.ForecastValue);
        var timeline=await host.Client.GetFromJsonAsync<List<TimelineItemDto>>($"/api/crm/timeline?customerId={convertedLead.CustomerId}");Assert.Contains(timeline!,x=>x.Subject.Contains("converted",StringComparison.OrdinalIgnoreCase));
        await using var scope=host.App.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();Assert.True(await db.SecurityAuditEvents.AnyAsync(x=>x.EventType=="CrmLeadCreated"&&x.CompanyId==LondonVipCompany.Id));Assert.True(await db.BusinessEvents.AnyAsync(x=>x.EventType=="CrmLeadConverted"&&x.CompanyId==LondonVipCompany.Id));
    }

    [Fact]
    public async Task IncomingCommunicationsCreateLeadThreadActivityAndPreventDuplicates()
    {
        await using var host=await TestApiHost.StartAsync();var request=new IncomingMessageRequest(CrmConversationChannel.WhatsApp,"thread-1","message-1","+447700900123","Airport transfer","Please quote Heathrow to London",DateTimeOffset.UtcNow);
        using var first=await host.Client.PostAsJsonAsync("/api/crm/communications/incoming",request);Assert.Equal(HttpStatusCode.Accepted,first.StatusCode);var conversation=await first.Content.ReadFromJsonAsync<ConversationDto>();Assert.NotNull(conversation);Assert.NotNull(conversation.LeadId);
        using var duplicate=await host.Client.PostAsJsonAsync("/api/crm/communications/incoming",request);Assert.Equal(HttpStatusCode.Accepted,duplicate.StatusCode);
        var inbox=await host.Client.GetFromJsonAsync<List<ConversationDto>>("/api/crm/conversations?q=Heathrow");var row=Assert.Single(inbox!);Assert.Equal(1,row.MessageCount);
        var timeline=await host.Client.GetFromJsonAsync<List<TimelineItemDto>>($"/api/crm/timeline?leadId={row.LeadId}");Assert.Contains(timeline!,x=>x.Type==CrmActivityType.WhatsApp);
    }

    [Fact]
    public async Task ReviewsCampaignsTasksArePersistedAndTenantScoped()
    {
        await using var host=await TestApiHost.StartAsync();
        Assert.Equal(HttpStatusCode.Created,(await host.Client.PostAsJsonAsync("/api/crm/tasks",new CrmTaskRequest(CrmTaskType.Callback,"Call prospect",null,null,null,null,null,DateTimeOffset.UtcNow.AddHours(1),null))).StatusCode);
        Assert.Equal(HttpStatusCode.Created,(await host.Client.PostAsJsonAsync("/api/crm/reviews",new CrmReviewRequest(CrmReviewSource.Google,"review-1",null,null,2,"Late pickup",true,null,DateTimeOffset.UtcNow))).StatusCode);
        Assert.Equal(HttpStatusCode.Created,(await host.Client.PostAsJsonAsync("/api/crm/campaigns",new CrmCampaignRequest("Airport accounts",CrmCampaignChannel.Email,"active corporate customers","Draft",100,DateTimeOffset.UtcNow.AddDays(1),null))).StatusCode);
        Assert.Single((await host.Client.GetFromJsonAsync<List<CrmTask>>("/api/crm/tasks"))!);Assert.Single((await host.Client.GetFromJsonAsync<List<CrmReview>>("/api/crm/reviews"))!);Assert.Single((await host.Client.GetFromJsonAsync<List<CrmCampaign>>("/api/crm/campaigns"))!);
        await using(var scope=host.App.Services.CreateAsyncScope()){var db=scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();var other=Guid.NewGuid();db.Companies.Add(NewCompany(other));db.CrmLeads.Add(new(){Id=Guid.NewGuid(),CompanyId=other,Reference="LEAD-OTHER",FirstName="Other",LastName="Tenant",Email="other@tenant.test",Source="Web",CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow});await db.SaveChangesAsync();}
        var leads=await host.Client.GetFromJsonAsync<List<CrmLeadDto>>("/api/crm/leads?q=other%40tenant.test");Assert.Empty(leads!);
    }

    [Fact]
    public async Task CrmAuthorizationAndValidationAreEnforced()
    {
        await using var host=await TestApiHost.StartAsync();using var anonymous=new HttpRequestMessage(HttpMethod.Get,"/api/crm/dashboard");anonymous.Headers.Add("X-Test-Anonymous","true");Assert.Equal(HttpStatusCode.Unauthorized,(await host.Client.SendAsync(anonymous)).StatusCode);
        using var financeWrite=new HttpRequestMessage(HttpMethod.Post,"/api/crm/leads"){Content=JsonContent.Create(NewLead("finance@example.test"))};financeWrite.Headers.Add("X-Test-Role",SecurityRoles.Finance);Assert.Equal(HttpStatusCode.Forbidden,(await host.Client.SendAsync(financeWrite)).StatusCode);
        using var invalid=await host.Client.PostAsJsonAsync("/api/crm/leads",NewLead(null) with{Phone=null});Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
    }
    private static CrmLeadRequest NewLead(string? email)=>new("Ada","Lovelace",email,"07700900123","Website",50,CrmLeadStatus.New,CrmPriority.High,25,500,DateTimeOffset.UtcNow.AddDays(30),DateTimeOffset.UtcNow.AddDays(1),"Airport account",null);
    private static Company NewCompany(Guid id)=>new(){Id=id,TradingName="Other Cars",LegalName="Other Cars Ltd",Slug=$"other-{id:N}",Email="office@other.test",Phone="07000000000",WebsiteUrl="",AddressLine1="1 Road",AddressLine2="",City="London",Postcode="SW1A 1AA",Country="GB",TimeZone="Europe/London",CurrencyCode="GBP",IsActive=true,CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow};
}

using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Accounting;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Infrastructure.Accounting;

public sealed class FinanceAdministrationService(LondonVIPDbContext db, ICompanyContext company) : IFinanceAdministrationService
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { ReferenceHandler = ReferenceHandler.IgnoreCycles };
    static readonly IReadOnlyDictionary<string, Type> Resources = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
    {
        ["accounts"] = typeof(LedgerAccount), ["suppliers"] = typeof(Supplier), ["supplier-invoices"] = typeof(SupplierInvoice), ["supplier-payments"] = typeof(SupplierPayment),
        ["expenses"] = typeof(Expense), ["budgets"] = typeof(Budget), ["fiscal-years"] = typeof(FiscalYear), ["periods"] = typeof(AccountingPeriod), ["bank-accounts"] = typeof(BankAccount),
        ["bank-rules"] = typeof(BankRule), ["bank-imports"] = typeof(BankImportBatch), ["credit-notes"] = typeof(CreditNote), ["recurring-invoices"] = typeof(RecurringInvoiceSchedule),
        ["supplier-credits"] = typeof(SupplierCredit), ["supplier-contracts"] = typeof(SupplierContract), ["supplier-documents"] = typeof(SupplierDocument),
        ["vat-returns"] = typeof(VatReturn), ["journals"] = typeof(Journal), ["journal-entries"] = typeof(JournalEntry), ["driver-settlements"] = typeof(DriverSettlement), ["posting-profiles"] = typeof(AccountingPostingProfile)
    };

    public async Task<FinanceAdminPage> QueryAsync(string resource, FinanceAdminQuery request, CancellationToken token = default)
    {
        var type = TypeFor(resource); var query = TenantQuery(type); var excluded = await db.FinanceRecordStates.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.ResourceType == resource && (x.IsDeleted || !request.IncludeArchived && x.IsArchived)).Select(x => x.ResourceId).ToListAsync(token);
        if (excluded.Count > 0) query = WhereIds(query, type, excluded, false);
        if (!string.IsNullOrWhiteSpace(request.Search)) query = Search(query, type, request.Search.Trim());
        var total = Count(query, type); query = Order(query, type, request.Sort, request.Descending); var page = Math.Max(1, request.Page); var size = Math.Clamp(request.PageSize, 1, 100); query = Page(query, type, (page - 1) * size, size);
        var items = new List<JsonElement>(); foreach (var value in query) items.Add(ToElement(value)); return new(items, page, size, total);
    }

    public async Task<JsonElement?> GetAsync(string resource, Guid id, CancellationToken token = default)
    {
        var value = await Find(resource, id, token); return value is null ? null : ToElement(value);
    }

    public async Task<JsonElement> CreateAsync(string resource, JsonElement value, string correlationId, CancellationToken token = default)
    {
        var type = TypeFor(resource); var entity = JsonSerializer.Deserialize(value.GetRawText(), type, Json) ?? throw new InvalidOperationException("The finance record is invalid."); Set(type, entity, "Id", Guid.NewGuid()); Set(type, entity, "CompanyId", company.CompanyId); await Validate(entity, token); db.Add(entity); await SaveHistory(resource, entity, "Created", null, correlationId, token); await db.SaveChangesAsync(token); return ToElement(entity);
    }

    public async Task<JsonElement?> UpdateAsync(string resource, Guid id, JsonElement changes, string correlationId, CancellationToken token = default)
    {
        var entity = await Find(resource, id, token); if (entity is null) return null; var before = Serialize(entity); Apply(entity, changes); await Validate(entity, token); await SaveHistory(resource, entity, "Updated", before, correlationId, token); await db.SaveChangesAsync(token); return ToElement(entity);
    }

    public async Task<int> BulkAsync(string resource, string action, FinanceBulkRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.Ids.Count is 0 or > 500) throw new InvalidOperationException("Bulk actions require between 1 and 500 records."); var count = 0;
        foreach (var id in request.Ids.Distinct())
        {
            var entity = await Find(resource, id, token); if (entity is null) continue; var before = Serialize(entity);
            switch (action.ToLowerInvariant())
            {
                case "edit": if (!request.Changes.HasValue) throw new InvalidOperationException("Bulk edit changes are required."); Apply(entity, request.Changes.Value); break;
                case "archive": await State(resource, id, true, false, token); break;
                case "restore": await State(resource, id, false, false, token); break;
                case "delete": await State(resource, id, false, true, token); break;
                case "approve": Approve(entity); break;
                case "export": break;
                default: throw new InvalidOperationException("Bulk action is unsupported.");
            }
            await SaveHistory(resource, entity, action, before, correlationId, token); count++;
        }
        await db.SaveChangesAsync(token); return count;
    }

    public async Task<IReadOnlyList<FinanceRecordHistory>> HistoryAsync(string resource, Guid id, CancellationToken token = default) => await db.FinanceRecordHistories.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.ResourceType == resource && x.ResourceId == id).OrderByDescending(x => x.CreatedAt).ToListAsync(token);

    async Task<object?> Find(string resource, Guid id, CancellationToken token) { var type = TypeFor(resource); var entity = await db.FindAsync(type, [id], token); if (entity is null || (Guid?)type.GetProperty("CompanyId")?.GetValue(entity) != company.CompanyId) return null; var state = await db.FinanceRecordStates.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == company.CompanyId && x.ResourceType == resource && x.ResourceId == id, token); return state?.IsDeleted == true ? null : entity; }
    async Task State(string resource, Guid id, bool archived, bool deleted, CancellationToken token) { var state = await db.FinanceRecordStates.SingleOrDefaultAsync(x => x.CompanyId == company.CompanyId && x.ResourceType == resource && x.ResourceId == id, token); if (state is null) { state = new() { Id = Guid.NewGuid(), CompanyId = company.CompanyId, ResourceType = resource, ResourceId = id }; db.Add(state); } state.IsArchived = archived; state.IsDeleted = deleted; state.UpdatedAt = DateTimeOffset.UtcNow; }
    async Task Validate(object entity, CancellationToken token)
    {
        if (entity is Expense expense && !ExpenseCategories.All.Contains(expense.Category.Trim())) throw new InvalidOperationException("Expense category is invalid.");
        var supplierId = entity switch { SupplierCredit x => x.SupplierId, SupplierContract x => x.SupplierId, SupplierDocument x => x.SupplierId, _ => (Guid?)null };
        if (supplierId.HasValue && !await db.Suppliers.AnyAsync(x => x.CompanyId == company.CompanyId && x.Id == supplierId.Value, token)) throw new InvalidOperationException("Supplier was not found.");
        if (entity is SupplierCredit credit && credit.SupplierInvoiceId.HasValue && !await db.SupplierInvoices.AnyAsync(x => x.CompanyId == company.CompanyId && x.SupplierId == credit.SupplierId && x.Id == credit.SupplierInvoiceId.Value, token)) throw new InvalidOperationException("Supplier invoice was not found.");
        if (entity is SupplierDocument document && document.SupplierContractId.HasValue && !await db.SupplierContracts.AnyAsync(x => x.CompanyId == company.CompanyId && x.SupplierId == document.SupplierId && x.Id == document.SupplierContractId.Value, token)) throw new InvalidOperationException("Supplier contract was not found.");
    }
    async Task SaveHistory(string resource, object entity, string action, string? before, string correlationId, CancellationToken token) { var id = (Guid)(entity.GetType().GetProperty("Id")?.GetValue(entity) ?? Guid.Empty); db.FinanceRecordHistories.Add(new() { Id = Guid.NewGuid(), CompanyId = company.CompanyId, ResourceType = resource, ResourceId = id, Action = action, BeforeJson = before, AfterJson = Serialize(entity), CorrelationId = correlationId, CreatedAt = DateTimeOffset.UtcNow }); await Task.CompletedTask; token.ThrowIfCancellationRequested(); }
    static void Apply(object entity, JsonElement changes) { if (changes.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Changes must be a JSON object."); var type = entity.GetType(); foreach (var item in changes.EnumerateObject()) { var property = type.GetProperties().FirstOrDefault(x => x.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase)); if (property is null || !property.CanWrite || property.Name is "Id" or "CompanyId" || !Scalar(property.PropertyType)) continue; property.SetValue(entity, JsonSerializer.Deserialize(item.Value.GetRawText(), property.PropertyType, Json)); } }
    static void Approve(object entity) { var property = entity.GetType().GetProperty("Status"); if (property is null || !property.CanWrite) throw new InvalidOperationException("This finance record has no approval workflow."); if (property.PropertyType == typeof(string)) property.SetValue(entity, "Approved"); else if (property.PropertyType.IsEnum && Enum.TryParse(property.PropertyType, "Approved", true, out var value)) property.SetValue(entity, value); else throw new InvalidOperationException("This finance record cannot be approved."); }
    static bool Scalar(Type value) { var type = Nullable.GetUnderlyingType(value) ?? value; return type.IsEnum || type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type == typeof(Guid) || type == typeof(DateOnly) || type == typeof(DateTimeOffset); }
    static void Set(Type type, object entity, string name, object value) { var property = type.GetProperty(name) ?? throw new InvalidOperationException($"{type.Name} is not tenant-owned."); property.SetValue(entity, value); }
    static Type TypeFor(string resource) => Resources.TryGetValue(resource, out var type) ? type : throw new InvalidOperationException("Finance resource is unsupported.");
    IQueryable TenantQuery(Type type) { var set = (IQueryable)typeof(DbContext).GetMethods().Single(x => x.Name == nameof(DbContext.Set) && x.IsGenericMethod && x.GetParameters().Length == 0).MakeGenericMethod(type).Invoke(db, null)!; var value = Expression.Parameter(type, "x"); var companyProperty = Expression.Property(value, "CompanyId"); return set.Provider.CreateQuery(Expression.Call(typeof(Queryable), nameof(Queryable.Where), [type], set.Expression, Expression.Lambda(Expression.Equal(companyProperty, Expression.Constant(company.CompanyId)), value))); }
    static IQueryable Search(IQueryable query, Type type, string term) { var value = Expression.Parameter(type, "x"); var candidates = type.GetProperties().Where(x => x.PropertyType == typeof(string)).Select(x => Expression.AndAlso(Expression.NotEqual(Expression.Property(value, x), Expression.Constant(null, typeof(string))), Expression.Call(Expression.Property(value, x), nameof(string.Contains), Type.EmptyTypes, Expression.Constant(term)))).ToList(); if (candidates.Count == 0) return query; var body = candidates.Aggregate(Expression.OrElse); return query.Provider.CreateQuery(Expression.Call(typeof(Queryable), nameof(Queryable.Where), [type], query.Expression, Expression.Lambda(body, value))); }
    static IQueryable WhereIds(IQueryable query, Type type, IReadOnlyList<Guid> ids, bool include) { var value = Expression.Parameter(type, "x"); var contains = Expression.Call(Expression.Constant(ids), ids.GetType().GetMethod(nameof(List<Guid>.Contains), [typeof(Guid)]) ?? typeof(ICollection<Guid>).GetMethod(nameof(ICollection<Guid>.Contains))!, Expression.Property(value, "Id")); Expression body = include ? contains : Expression.Not(contains); return query.Provider.CreateQuery(Expression.Call(typeof(Queryable), nameof(Queryable.Where), [type], query.Expression, Expression.Lambda(body, value))); }
    static IQueryable Order(IQueryable query, Type type, string? sort, bool descending) { var property = type.GetProperties().FirstOrDefault(x => x.Name.Equals(sort, StringComparison.OrdinalIgnoreCase)) ?? type.GetProperty("CreatedAt") ?? type.GetProperty("Id")!; var value = Expression.Parameter(type, "x"); return query.Provider.CreateQuery(Expression.Call(typeof(Queryable), descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy), [type, property.PropertyType], query.Expression, Expression.Lambda(Expression.Property(value, property), value))); }
    static IQueryable Page(IQueryable query, Type type, int skip, int take) { var skipped = query.Provider.CreateQuery(Expression.Call(typeof(Queryable), nameof(Queryable.Skip), [type], query.Expression, Expression.Constant(skip))); return skipped.Provider.CreateQuery(Expression.Call(typeof(Queryable), nameof(Queryable.Take), [type], skipped.Expression, Expression.Constant(take))); }
    static int Count(IQueryable query, Type type) => (int)query.Provider.Execute(Expression.Call(typeof(Queryable), nameof(Queryable.Count), [type], query.Expression))!;
    static string Serialize(object value) => JsonSerializer.Serialize(value, value.GetType(), Json);
    static JsonElement ToElement(object value) => JsonSerializer.SerializeToElement(value, value.GetType(), Json);
}

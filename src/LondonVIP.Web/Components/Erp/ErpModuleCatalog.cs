namespace LondonVIP.Web.Components.Erp;

public static class ErpModuleCatalog
{
    public static readonly IReadOnlyList<ErpModuleDefinition> All =
    [
        M("Operations", "Dashboard", "/erp/dashboard", "A single operational view of today's service.", "Live KPIs", "Operational alerts", "Daily performance"),
        M("Operations", "Quotes", "/erp/quotes", "Review and manage customer journey quotations.", "Quote pipeline", "Price review", "Quote conversion"),
        M("Operations", "Bookings", "/erp/bookings", "Coordinate confirmed and upcoming passenger journeys.", "Booking register", "Journey details", "Status workflow"),
        M("Operations", "Dispatch", "/erp/dispatch", "Assign work and monitor the daily operations board.", "Driver assignment", "Dispatch board", "Job status"),
        M("Operations", "Airport Operations / Flights", "/erp/airport-operations", "Coordinate airport collections and arrival activity.", "Arrival board", "Waiting windows", "Pickup coordination"),

        M("CRM", "Leads & CRM", "/erp/leads", "Track opportunities from first enquiry through conversion.", "Source and campaign", "Landing page attribution", "Quote and booking linkage", "Won/lost lead status", "Follow-up tasks", "Conversion reporting"),
        M("CRM", "Customers", "/erp/customers", "Maintain a consistent view of passenger relationships.", "Customer directory", "Journey history", "Preferences"),
        M("CRM", "Corporate Accounts", "/erp/corporate-accounts", "Support account-managed business travel customers.", "Account profiles", "Booker contacts", "Travel policies"),
        M("CRM", "Marketing", "/erp/marketing", "Plan responsible customer communications and campaigns.", "Campaign planning", "Audience lists", "Performance summaries"),
        M("CRM", "Communications", "/erp/communications", "Prepare operational and customer message templates.", "Message templates", "Delivery history", "Service notices"),

        M("Fleet & Drivers", "Drivers", "/erp/drivers", "Manage the active driver network and availability.", "Driver directory", "Availability", "Performance overview"),
        M("Fleet & Drivers", "Driver Dashboard", "/erp/driver-dashboard", "Operate a driver's current and upcoming workload.", "Current job", "Quick actions", "Daily workload"),
        M("Fleet & Drivers", "Fleet", "/erp/fleet", "Maintain vehicles, capacity and operating status.", "Vehicle register", "Capacity", "Maintenance status"),
        M("Fleet & Drivers", "Driver Accounts", "/erp/driver-accounts", "Prepare statements and account-level driver records.", "Account summary", "Commission statements", "Weekly account status"),

        M("Finance", "Pricing", "/erp/pricing", "Configure tenant-owned pricing rules and supplements.", "Pricing rules", "Airport supplements", "Waiting charges"),
        M("Finance", "Invoicing", "/erp/invoicing", "Prepare customer and corporate invoicing workflows.", "Invoice register", "Draft workflow", "VAT summaries"),
        M("Finance", "Payments", "/erp/payments", "A future home for payment status and reconciliation.", "Payment status", "Reconciliation", "Refund tracking"),
        M("Finance", "Reports & BI", "/erp/reports", "Turn operational and financial activity into decisions.", "Revenue reporting", "Journey performance", "Exportable dashboards"),

        M("Digital", "Website / CMS", "/erp/website-cms", "Manage the public website content without changing code.", "Homepage management", "Airport pages", "Local area pages", "SEO metadata", "Promotions", "Testimonials", "FAQs"),
        M("Digital", "Insights / Travel Hub", "/erp/insights", "Publish useful, locally relevant travel intelligence.", "TfL / TPH updates", "Airport news", "Hammersmith & Fulham travel insights", "Road closures", "Travel guides", "SEO articles"),
        M("Digital", "Live Journey Intelligence", "/erp/journey-intelligence", "Bring real-time journey context into operations when integrations are enabled.", "Live traffic", "Route ETA", "Airport destination journey conditions", "Flight tracking", "Journey-risk alerts", "Customer messaging"),

        M("Support & Compliance", "Complaints / Support", "/erp/support", "Track service issues and customer resolutions.", "Support cases", "Ownership", "Resolution tracking"),
        M("Support & Compliance", "Documents & Compliance", "/erp/documents", "Monitor operational documents and expiry dates.", "Document register", "Expiry alerts", "Compliance status"),
        M("Support & Compliance", "Audit Logs", "/erp/audit-logs", "Review important platform activity and changes.", "Change history", "Actor details", "Exportable records"),

        M("Platform", "Automation", "/erp/automation", "Manage scheduled workflows, business events, reminders and escalations.", "Workflow dashboard", "Scheduled jobs", "Rule engine"),
        M("Platform", "Company Setup", "/erp/company-setup", "Configure the current company's profile, branding and defaults.", "Company profile", "Operational settings", "Branding"),
        M("Platform", "Users & Security", "/erp/users-security", "Prepare access management for a later authentication phase.", "User directory", "Roles", "Access policies"),
        M("Platform", "Integrations", "/erp/integrations", "Configure future external service connections.", "Connection catalogue", "Credentials management", "Health status"),
        M("Platform", "System Settings", "/erp/system-settings", "Manage reusable platform behaviour and defaults.", "System defaults", "Feature settings", "Operational preferences")
    ];

    public static ErpModuleDefinition? Find(string absolutePath)
    {
        var path = absolutePath.TrimEnd('/');
        if (path.Length == 0) path = "/erp";
        return All.FirstOrDefault(module => string.Equals(module.Route, path, StringComparison.OrdinalIgnoreCase));
    }

    private static ErpModuleDefinition M(string category, string title, string route, string description, params string[] features) =>
        new(category, title, route, description, features);
}

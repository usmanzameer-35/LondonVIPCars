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
        M("Operations", "Maps", "/erp/map", "Monitor tenant-owned drivers and journeys geographically.", "Live driver map", "Journey routes", "Operational search"),
        M("Operations", "Live Tracking", "/erp/live-tracking", "Monitor fresh driver GPS positions and current journey progress.", "Online status", "Live ETA", "Journey progress"),
        M("Operations", "Journey Replay", "/erp/journey-replay", "Review timestamped journey history for operational investigation.", "GPS history", "Route replay", "Timeline"),

        M("CRM", "Dashboard", "/erp/crm", "CRM performance, workload and communication health.", "Lead summary", "Forecast", "Response workload"),
        M("CRM", "Leads", "/erp/leads", "Track enquiries from first contact through conversion.", "Lead scoring", "Ownership", "Follow-up"),
        M("CRM", "Opportunities", "/erp/opportunities", "Manage qualified revenue opportunities.", "Expected revenue", "Probability", "Close dates"),
        M("CRM", "Sales Pipeline", "/erp/sales-pipeline", "Move opportunities through configurable stages.", "Stages", "Forecast", "Win/loss reasons"),
        M("CRM", "Customers", "/erp/customers", "Maintain a consistent view of passenger relationships.", "Customer directory", "Journey history", "Preferences"),
        M("CRM", "Corporate Accounts", "/erp/corporate-accounts", "Support account-managed business travel customers.", "Account profiles", "Booker contacts", "Travel policies"),
        M("CRM", "Schools", "/erp/crm/schools", "Manage school travel relationships.", "Accounts", "Contacts", "Activity"),
        M("CRM", "Hotels", "/erp/crm/hotels", "Manage hotel and concierge relationships.", "Accounts", "Contacts", "Activity"),
        M("CRM", "NHS", "/erp/crm/nhs", "Manage healthcare transport accounts.", "Accounts", "Contacts", "Activity"),
        M("CRM", "Travel Agents", "/erp/crm/travel-agents", "Manage travel trade partners.", "Accounts", "Contacts", "Activity"),
        M("CRM", "Law Firms", "/erp/crm/law-firms", "Manage legal-sector accounts.", "Accounts", "Contacts", "Activity"),
        M("CRM", "Account Managers", "/erp/crm/account-managers", "Review CRM ownership and performance.", "Owners", "Pipeline", "Activity"),
        M("CRM", "CRM Quotations", "/erp/crm/quotations", "Manage sales quotations.", "Quotes", "Acceptance", "Conversion"),
        M("CRM", "Tasks", "/erp/crm/tasks", "Manage calls, meetings, callbacks and reminders.", "Assignments", "Due dates", "Recurrence"),
        M("CRM", "Calendar", "/erp/crm/calendar", "Review scheduled CRM work.", "Meetings", "Callbacks", "Reminders"),
        M("CRM", "Activities", "/erp/crm/activities", "Review chronological customer activity.", "Timeline", "Resources", "Ownership"),
        M("CRM", "Documents", "/erp/crm/documents", "Manage CRM document metadata and versions.", "Contracts", "POs", "Version history"),
        M("CRM", "Notes", "/erp/crm/notes", "Review internal customer and sales notes.", "Customers", "Leads", "Accounts"),
        M("CRM", "Campaigns", "/erp/campaigns", "Plan responsible customer communications and campaigns.", "Campaign planning", "Audience lists", "ROI"),
        M("CRM", "Reviews", "/erp/reviews", "Manage reviews, complaints and resolutions.", "Ratings", "Complaints", "Resolution"),
        M("CRM", "CRM Reports", "/erp/crm/reports", "Review sales and customer performance.", "Conversion", "Revenue by source", "Lifetime value"),
        M("CRM", "Communications", "/erp/communications", "Prepare operational and customer message templates.", "Message templates", "Delivery history", "Service notices"),

        M("Growth", "Marketing Overview", "/erp/marketing", "Measure acquisition, retention and campaign performance.", "Growth KPIs", "Campaign ROI", "Lead sources"),
        M("Growth", "Promotions", "/erp/promotions", "Manage eligible, scheduled and usage-limited offers.", "Promo codes", "Eligibility", "Redemptions"),
        M("Growth", "Referrals", "/erp/referrals", "Track customer, driver and corporate referrals.", "Referral codes", "Qualification", "Rewards"),
        M("Growth", "Loyalty", "/erp/loyalty", "Operate points, tiers and reward history.", "Points ledger", "Tiers", "Vouchers"),
        M("Growth", "Newsletters", "/erp/newsletters", "Manage consented subscribers, lists and segments.", "Double opt-in", "Segmentation", "Unsubscribe"),
        M("Growth", "Content", "/erp/content", "Manage reusable website pages and SEO metadata.", "Content blocks", "Publishing", "Landing pages"),
        M("Growth", "Blog", "/erp/blog", "Publish scheduled, searchable travel content.", "Articles", "Categories", "SEO"),
        M("Growth", "SEO", "/erp/seo", "Manage redirects and search visibility controls.", "Metadata", "Redirects", "Health"),
        M("Growth", "Social", "/erp/social", "Coordinate provider-neutral social publishing.", "Drafts", "Scheduling", "Engagement"),
        M("Growth", "Media", "/erp/media", "Catalog versioned marketing and website assets.", "Folders", "Tags", "Versions"),
        M("Growth", "Analytics", "/erp/analytics", "Review growth and conversion performance.", "Acquisition", "Revenue", "ROI"),

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

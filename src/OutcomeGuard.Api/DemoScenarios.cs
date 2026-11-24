namespace OutcomeGuard.Api;

public record DemoDefinition(BusinessApp App, Rule Rule, string Prefix, decimal Impact, decimal Increment,
    string Description, string FailureType, string FailureReason);

public static class DemoScenarios
{
    public static DemoDefinition Get(string scenario) => scenario switch
    {
        "commerce" => new(new BusinessApp("demo-commerce", "Northstar Commerce", "E-commerce", "A paid order must become a fulfilled order."),
            new Rule("rule-demo-commerce", "demo-commerce", "Every paid order gets fulfilled", "Reserve inventory and create a shipment after payment.",
                "payment.captured", ["inventory.reserved", "shipment.created"], 15, "critical", "amount", "INR", "Fulfilment operations", true, default),
            "ORD", 4999, 1200, "Synthetic paid order", "fulfilment.failed", "Warehouse queue rejected the synthetic shipment request."),
        "events" => new(new BusinessApp("demo-events", "Gather Events", "Event registration", "Every registered attendee receives their pass and confirmation."),
            new Rule("rule-demo-events", "demo-events", "Every registration gets a usable pass", "Issue an entry pass and send a confirmation after registration.",
                "registration.completed", ["ticket.issued", "confirmation.sent"], 5, "warning", "attendees", "people", "Event experience team", true, default),
            "REG", 1, 0, "Synthetic event registration", "notification.failed", "Notification provider rejected the synthetic confirmation."),
        "hr" => new(new BusinessApp("demo-hr", "PeopleOps", "HR offboarding", "Offboarding must remove access and terminate active sessions."),
            new Rule("rule-demo-hr", "demo-hr", "Offboarding removes every access path", "Revoke access and terminate sessions after an employee is offboarded.",
                "employee.offboarded", ["access.revoked", "sessions.terminated"], 10, "critical", "people", "people", "IT operations", true, default),
            "OFF", 1, 0, "Synthetic employee offboarding", "identity-sync.failed", "Session termination job failed in the synthetic identity provider."),
        _ => throw new DomainException(404, "Choose commerce, events or hr.")
    };
}

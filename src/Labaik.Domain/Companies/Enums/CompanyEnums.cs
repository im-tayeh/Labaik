namespace Labaik.Domain.Companies.Enums;

// Business category chosen on the admin "create company" form. Labels are localized by the clients.
public enum CompanyCategory { Umrah = 1, Hajj = 2, HajjAndUmrah = 3 }

// Billing cycle of the contract. Stored only; billing is not enforced.
public enum SubscriptionPeriod { Month = 1, ThreeMonths = 2, SixMonths = 3, Year = 4 }

// Computed from IsStopped and the contract end date, never stored.
public enum CompanyStatus { Active = 1, Stopped = 2, Expired = 3 }

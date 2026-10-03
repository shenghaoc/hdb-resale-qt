namespace HdbResale.Domain;

public static class Fixture
{
    public static IReadOnlyList<ResaleTransaction> Transactions { get; } = Array.AsReadOnly<ResaleTransaction>([
        new("TP-1", "Tampines", "101 Tampines Street 11", "4 ROOM", 580_000, 1.3465, 103.9453),
        new("TP-2", "Tampines", "201 Tampines Street 21", "3 ROOM", 420_000, 1.3530, 103.9515),
        new("CL-1", "Clementi", "401 Clementi Avenue 1", "5 ROOM", 850_000, 1.3091, 103.7690),
        new("CL-2", "Clementi", "301 Clementi Avenue 4", "3 ROOM", 470_000, 1.3198, 103.7665),
        new("AM-1", "Ang Mo Kio", "101 Ang Mo Kio Avenue 3", "4 ROOM", 620_000, 1.3700, 103.8440),
        new("AM-2", "Ang Mo Kio", "201 Ang Mo Kio Avenue 6", "3 ROOM", 450_000, 1.3780, 103.8385)
    ]);
}

using TestModule.Backend.Contracts;

namespace TestModule.Backend.Helpers;

internal class SeedData
{
    public static List<SimpleDataTypes> DefaultSeedDataSimpleDataTypes()
        =>
        [
                new SimpleDataTypes()
                {
                    Id = 1,
                    MachineId = Guid.NewGuid(),
                    MachineDescription = "Presse für Stoßfänger",
                    MachineName = "Presse T1000",
                    Users = 4,
                    Built = new DateTimeOffset(2001, 2, 12, 0, 0, 0, new()),
                    Created = new DateTimeOffset(2012, 12, 12, 0, 0, 0, new())
                },
                new SimpleDataTypes()
                {
                    Id = 2,
                    MachineId = Guid.NewGuid(),
                    MachineDescription = "Presse für Stoßfänger - T2000",
                    MachineName = "Presse T2000",
                    Users = 3,
                    Built = new DateTimeOffset(2010, 6, 10, 0, 0, 0, new()),
                    Created = DateTimeOffset.UtcNow
                },
                new SimpleDataTypes()
                {
                    Id = 3,
                    MachineId = Guid.NewGuid(),
                    MachineDescription = "Presse für Stoßfänger - T5000",
                    MachineName = "Presse T5000",
                    Users = 1,
                    Built = new DateTimeOffset (2011, 11, 30, 0, 0, 0, new()),
                    Created = new DateTimeOffset (2017, 1, 09, 0, 0, 0, new())
                }
        ];

    public static List<SimpleEmployees> DefaultDataSimpleEmployees()
        =>
        [
            new SimpleEmployees()
            {
                emp_no = 1,
                Name = "Hans.Wurst",
                Salutaion = SimpleEmployees.Gender.Mr
            },
            new SimpleEmployees()
            {
                emp_no = 2,
                Name = "Karla.Kolumna",
                Salutaion = SimpleEmployees.Gender.Mrs
            }
        ];
}

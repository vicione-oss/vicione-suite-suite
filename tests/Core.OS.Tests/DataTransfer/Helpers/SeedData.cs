using System.Globalization;
using TestModule.Backend.Contracts;
using TestModule.Backend.DbContext;
using static TestModule.Backend.Contracts.Employees;

namespace Core.OS.Tests.DataTransfer.Helpers;

public enum TitleCase
{
    Default,
    CancelInsert,
    Execute
}

internal class SeedData
{
    public const string TitleWithSingleQuote = "Assistant Engineer''";
    public const string TitleWithDoubleQuote = "Assistant Engineer\"\"";
    public const string TitleWithSingleQuoteAndRoundBracket = "Staff')";
    public const string TitleWithSingleQuoteRoundBrackeAndSemicolon = "Senior Engineer');";
    public const string TitleWithBackslashSingleQuote = "Engineer\'";
    public const string TitleWithBackStickAndRoundBracket = "Staff`)";
    public const string TitleWithExecuteStatement = "'x','1966-03-28','1966-02-27'); DELETE FROM DepartmentManager; --";

    public static List<SimpleDataTypes> DefaultSeedDataSimpleDataTypes()
        =>
        [
            new SimpleDataTypes()
            {
                Id = 4,
                MachineId = Guid.NewGuid(),
                MachineDescription = "Presse für Stoßfänger - XP1",
                MachineName = "Presse XP1",
                Users = 4,
                Built = new DateTimeOffset(2001, 4, 1, 0, 0, 0, new()),
                Created = DateTimeOffset.UtcNow
            },
            new SimpleDataTypes()
            {
                Id = 5,
                MachineId = Guid.NewGuid(),
                MachineDescription = "Presse für Stoßfänger - XP2",
                MachineName = "Presse XP2",
                Users = 3,
                Built = new DateTimeOffset(2010, 6, 10, 0, 0, 0, new()),
                Created = DateTimeOffset.UtcNow
            },
            new SimpleDataTypes()
            {
                Id = 6,
                MachineId = Guid.NewGuid(),
                MachineDescription = "Presse für Stoßfänger - XP5",
                MachineName = "Presse XP5",
                Users = 1,
                Built = new DateTimeOffset(2011, 11, 30, 0, 0, 0, new()),
                Created = DateTimeOffset.UtcNow
            }
        ];

    public static void SeedMasterDbData(IReferenceDbContext dbSourceContext, TitleCase titleCase = TitleCase.Default)
    {
        dbSourceContext.Employees.AddRange(GetSomeEmployees());
        dbSourceContext.Departments.AddRange(GetDepartments());
        dbSourceContext.DepartmentEmployees.AddRange(GetDepartmentEmployees());
        dbSourceContext.DepartmentManagers.AddRange(GetDepartmentManager());
        dbSourceContext.Titles.AddRange(GetTitles(titleCase));
        dbSourceContext.Salaries.AddRange(GetSalaries());
        dbSourceContext.Instance.SaveChanges();
    }

    private static List<Employees> GetSomeEmployees()
        =>
        [
            new () { emp_no = 10001, birth_date = ToDateTimeUtc("1953-09-02"), first_name = "Georgi", last_name = "Facello", gender = Gender.M, hire_date = ToDateTimeUtc("1986-06-26")},
            new () { emp_no = 10002, birth_date = ToDateTimeUtc("1964-06-02"), first_name = "Bezalel", last_name = "Simmel", gender = Gender.F, hire_date = ToDateTimeUtc("1985-11-21")},
            new () { emp_no = 10003, birth_date = ToDateTimeUtc("1959-12-03"), first_name = "Parto", last_name = "Bamford", gender = Gender.M, hire_date = ToDateTimeUtc("1986-08-28")},
            new () { emp_no = 10004, birth_date = ToDateTimeUtc("1954-05-01"), first_name = "Chirstian", last_name = "Koblick", gender = Gender.M, hire_date = ToDateTimeUtc("1986-12-01")},
            new () { emp_no = 10005, birth_date = ToDateTimeUtc("1955-01-21"), first_name = "Kyoichi", last_name = "Maliniak", gender = Gender.M, hire_date = ToDateTimeUtc("1989-09-12")},
            new () { emp_no = 10006, birth_date = ToDateTimeUtc("1953-04-20"), first_name = "Anneke", last_name = "Preusig", gender = Gender.F, hire_date = ToDateTimeUtc("1989-06-02")},
            new () { emp_no = 10007, birth_date = ToDateTimeUtc("1957-05-23"), first_name = "Tzvetan", last_name = "Zielinski", gender = Gender.F, hire_date = ToDateTimeUtc("1989-02-10")},
            new () { emp_no = 10008, birth_date = ToDateTimeUtc("1958-02-19"), first_name = "Saniya", last_name = "Kalloufi", gender = Gender.M, hire_date = ToDateTimeUtc("1994-09-15")},
            new () { emp_no = 10009, birth_date = ToDateTimeUtc("1952-04-19"), first_name = "Sumant", last_name = "Peac", gender = Gender.F, hire_date = ToDateTimeUtc("1985-02-18")},
            new () { emp_no = 10010, birth_date = ToDateTimeUtc("1963-06-01"), first_name = "Duangkaew", last_name = "Piveteau", gender = Gender.F, hire_date = ToDateTimeUtc("1989-08-24")},
            new () { emp_no = 10011, birth_date = ToDateTimeUtc("1953-11-07"), first_name = "Mary", last_name = "Sluis", gender = Gender.F, hire_date = ToDateTimeUtc("1990-01-22")},
            new () { emp_no = 10012, birth_date = ToDateTimeUtc("1960-10-04"), first_name = "Patricio", last_name = "Bridgland", gender = Gender.M, hire_date = ToDateTimeUtc("1992-12-18")},
            new () { emp_no = 10013, birth_date = ToDateTimeUtc("1963-06-07"), first_name = "Eberhardt", last_name = "Terkki", gender = Gender.M, hire_date = ToDateTimeUtc("1985-10-20")},
            new () { emp_no = 20148, birth_date = ToDateTimeUtc("1966-03-28"), first_name = "Leo", last_name = "Leike", gender = Gender.M, hire_date = ToDateTimeUtc("1988-06-02")},
            new () { emp_no = 20350, birth_date = ToDateTimeUtc("1966-02-27"), first_name = "Emmi", last_name = "Rothner", gender = Gender.F, hire_date = ToDateTimeUtc("1991-03-18")},
        ];

    private static List<Departments> GetDepartments()
        =>
        [
            new () {dept_no = "d001", dept_name = "Marketing"},
            new () {dept_no = "d002", dept_name = "Finance"},
            new () {dept_no = "d003", dept_name = "Human Resources"},
            new () {dept_no = "d004", dept_name = "Production"},
            new () {dept_no = "d005", dept_name = "Development"},
            new () {dept_no = "d006", dept_name = "Quality Management"},
            new () {dept_no = "d007", dept_name = "Sales"},
            new () {dept_no = "d008", dept_name = "Research"},
            new () {dept_no = "d009", dept_name = "Customer Service"},
        ];

    private static List<DepartmentManager> GetDepartmentManager()
        =>
        [
            new () {emp_no = 10003, dept_no = "d001", from_date = ToDateTimeUtc("1991-10-01"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 20148, dept_no = "d004", from_date = ToDateTimeUtc("1992-08-02"), to_date = ToDateTimeUtc("1996-08-30")},
            new () {emp_no = 20350, dept_no = "d004", from_date = ToDateTimeUtc("1996-08-30"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10007, dept_no = "d009", from_date = ToDateTimeUtc("1985-01-01"), to_date = ToDateTimeUtc("1988-10-17")},
            new () {emp_no = 10011, dept_no = "d009", from_date = ToDateTimeUtc("1988-10-17"), to_date = ToDateTimeUtc("1996-01-03")},
            new () {emp_no = 10012, dept_no = "d009", from_date = ToDateTimeUtc("1996-01-03"), to_date = ToDateTimeUtc("9999-01-01")},
        ];

    private static List<DepartmentEmployees> GetDepartmentEmployees()
        =>
        [
            new () {emp_no = 10001, dept_no = "d001", from_date = ToDateTimeUtc("1986-06-26"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10002, dept_no = "d001", from_date = ToDateTimeUtc("1996-08-03"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10004, dept_no = "d004", from_date = ToDateTimeUtc("1986-12-01"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10005, dept_no = "d004", from_date = ToDateTimeUtc("1996-11-24"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10006, dept_no = "d004", from_date = ToDateTimeUtc("1992-07-29"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10008, dept_no = "d009", from_date = ToDateTimeUtc("1988-06-06"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10009, dept_no = "d009", from_date = ToDateTimeUtc("1994-12-22"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10010, dept_no = "d009", from_date = ToDateTimeUtc("1994-10-06"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10013, dept_no = "d009", from_date = ToDateTimeUtc("1998-09-09"), to_date = ToDateTimeUtc("9999-01-01")},
        ];

    private static List<Titles> GetTitles(TitleCase titleCase = TitleCase.Default)
        =>
        [
            new () {emp_no = 10001, title = "Senior Engineer", from_date = ToDateTimeUtc("1986-06-26"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10002, title = "Staff", from_date = ToDateTimeUtc("1996-08-03"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10003, title = "Senior Engineer", from_date = ToDateTimeUtc("1995-12-03"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10004, title = "Engineer", from_date = ToDateTimeUtc("1986-12-01"), to_date = ToDateTimeUtc("1995-12-01")},
            new () {emp_no = 10004, title = "Senior Engineer", from_date = ToDateTimeUtc("1995-12-01"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10005, title = "Senior Staff", from_date = ToDateTimeUtc("1996-09-12"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10005, title = "Staff", from_date = ToDateTimeUtc("1989-09-12"), to_date = ToDateTimeUtc("1996-09-12")},
            new () {emp_no = 10006, title = "Senior Engineer", from_date = ToDateTimeUtc("1990-08-05"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10007, title = "Senior Staff", from_date = ToDateTimeUtc("1996-02-11"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10007, title = "Staff", from_date = ToDateTimeUtc("1989-02-10"), to_date = ToDateTimeUtc("1996-02-11")},
            new () {emp_no = 10008, from_date = ToDateTimeUtc("1998-03-11"), to_date = ToDateTimeUtc("2000-07-31"),
                title = (titleCase == TitleCase.CancelInsert) ? TitleWithSingleQuote : "Assistant Engineer"},
            new () {emp_no = 10009, from_date = ToDateTimeUtc("1985-02-18"), to_date = ToDateTimeUtc("1990-02-18"),
                title = (titleCase == TitleCase.CancelInsert) ? TitleWithDoubleQuote : "Assistant Engineer"},
            new () {emp_no = 10009, from_date = ToDateTimeUtc("1990-02-18"), to_date = ToDateTimeUtc("1995-02-18"),
                title = (titleCase == TitleCase.CancelInsert) ? TitleWithSingleQuoteAndRoundBracket :
                        (titleCase == TitleCase.Execute) ? TitleWithExecuteStatement : "Engineer"},
            new () {emp_no = 10009, from_date = ToDateTimeUtc("1995-02-18"), to_date = ToDateTimeUtc("9999-01-01"),
                title = (titleCase == TitleCase.CancelInsert) ? TitleWithSingleQuoteRoundBrackeAndSemicolon : "Senior Engineer"},
            new () {emp_no = 10010, from_date = ToDateTimeUtc("1996-11-24"), to_date = ToDateTimeUtc("9999-01-01"),
                title = (titleCase == TitleCase.CancelInsert) ? TitleWithBackslashSingleQuote : "Engineer"},
            new () {emp_no = 10011, from_date = ToDateTimeUtc("1990-01-22"), to_date = ToDateTimeUtc("1996-11-09"),
                title = (titleCase == TitleCase.CancelInsert) ? TitleWithBackStickAndRoundBracket : "Staff"},
            new () {emp_no = 10012, title = "Engineer", from_date = ToDateTimeUtc("1992-12-18"), to_date = ToDateTimeUtc("2000-12-18")},
            new () {emp_no = 10012, title = "Senior Engineer", from_date = ToDateTimeUtc("2000-12-18"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10013, title = "Senior Staff", from_date = ToDateTimeUtc("1985-10-20"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 20148, title = "Engineer", from_date = ToDateTimeUtc("1993-12-29"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 20148, title = "Senior Staff", from_date = ToDateTimeUtc("1992-09-19"), to_date = ToDateTimeUtc("1993-08-22")},
            new () {emp_no = 20350, title = "Staff", from_date = ToDateTimeUtc("1998-02-11"), to_date = ToDateTimeUtc("9999-01-01")},
        ];

    private static List<Salaries> GetSalaries()
        =>
        [
            new () {emp_no = 10001, salary = 60117, from_date = ToDateTimeUtc("1986-06-26"), to_date = ToDateTimeUtc("1987-06-26")},
            new () {emp_no = 10001, salary = 62102, from_date = ToDateTimeUtc("1987-06-26"), to_date = ToDateTimeUtc("1988-06-25")},
            new () {emp_no = 10001, salary = 66074, from_date = ToDateTimeUtc("1988-06-25"), to_date = ToDateTimeUtc("1989-06-25")},
            new () {emp_no = 10001, salary = 66596, from_date = ToDateTimeUtc("1989-06-25"), to_date = ToDateTimeUtc("1990-06-25")},
            new () {emp_no = 10001, salary = 66961, from_date = ToDateTimeUtc("1990-06-25"), to_date = ToDateTimeUtc("1991-06-25")},
            new () {emp_no = 10001, salary = 71046, from_date = ToDateTimeUtc("1991-06-25"), to_date = ToDateTimeUtc("1992-06-24")},
            new () {emp_no = 10001, salary = 74333, from_date = ToDateTimeUtc("1992-06-24"), to_date = ToDateTimeUtc("1993-06-24")},
            new () {emp_no = 10001, salary = 75286, from_date = ToDateTimeUtc("1993-06-24"), to_date = ToDateTimeUtc("1994-06-24")},
            new () {emp_no = 10001, salary = 75994, from_date = ToDateTimeUtc("1994-06-24"), to_date = ToDateTimeUtc("1995-06-24")},
            new () {emp_no = 10001, salary = 76884, from_date = ToDateTimeUtc("1995-06-24"), to_date = ToDateTimeUtc("1996-06-23")},
            new () {emp_no = 10001, salary = 80013, from_date = ToDateTimeUtc("1996-06-23"), to_date = ToDateTimeUtc("1997-06-23")},
            new () {emp_no = 10001, salary = 81025, from_date = ToDateTimeUtc("1997-06-23"), to_date = ToDateTimeUtc("1998-06-23")},
            new () {emp_no = 10001, salary = 81097, from_date = ToDateTimeUtc("1998-06-23"), to_date = ToDateTimeUtc("1999-06-23")},
            new () {emp_no = 10001, salary = 84917, from_date = ToDateTimeUtc("1999-06-23"), to_date = ToDateTimeUtc("2000-06-22")},
            new () {emp_no = 10001, salary = 85112, from_date = ToDateTimeUtc("2000-06-22"), to_date = ToDateTimeUtc("2001-06-22")},
            new () {emp_no = 10001, salary = 85097, from_date = ToDateTimeUtc("2001-06-22"), to_date = ToDateTimeUtc("2002-06-22")},
            new () {emp_no = 10001, salary = 88958, from_date = ToDateTimeUtc("2002-06-22"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10002, salary = 65828, from_date = ToDateTimeUtc("1996-08-03"), to_date = ToDateTimeUtc("1997-08-03")},
            new () {emp_no = 10002, salary = 65909, from_date = ToDateTimeUtc("1997-08-03"), to_date = ToDateTimeUtc("1998-08-03")},
            new () {emp_no = 10002, salary = 67534, from_date = ToDateTimeUtc("1998-08-03"), to_date = ToDateTimeUtc("1999-08-03")},
            new () {emp_no = 10002, salary = 69366, from_date = ToDateTimeUtc("1999-08-03"), to_date = ToDateTimeUtc("2000-08-02")},
            new () {emp_no = 10002, salary = 71963, from_date = ToDateTimeUtc("2000-08-02"), to_date = ToDateTimeUtc("2001-08-02")},
            new () {emp_no = 10002, salary = 72527, from_date = ToDateTimeUtc("2001-08-02"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10003, salary = 40006, from_date = ToDateTimeUtc("1995-12-03"), to_date = ToDateTimeUtc("1996-12-02")},
            new () {emp_no = 10003, salary = 43616, from_date = ToDateTimeUtc("1996-12-02"), to_date = ToDateTimeUtc("1997-12-02")},
            new () {emp_no = 10003, salary = 43466, from_date = ToDateTimeUtc("1997-12-02"), to_date = ToDateTimeUtc("1998-12-02")},
            new () {emp_no = 10003, salary = 43636, from_date = ToDateTimeUtc("1998-12-02"), to_date = ToDateTimeUtc("1999-12-02")},
            new () {emp_no = 10003, salary = 43478, from_date = ToDateTimeUtc("1999-12-02"), to_date = ToDateTimeUtc("2000-12-01")},
            new () {emp_no = 10003, salary = 43699, from_date = ToDateTimeUtc("2000-12-01"), to_date = ToDateTimeUtc("2001-12-01")},
            new () {emp_no = 10003, salary = 43311, from_date = ToDateTimeUtc("2001-12-01"), to_date = ToDateTimeUtc("9999-01-01")},
            new () {emp_no = 10004, salary = 40054, from_date = ToDateTimeUtc("1986-12-01"), to_date = ToDateTimeUtc("1987-12-01")},
            new () {emp_no = 10004, salary = 42283, from_date = ToDateTimeUtc("1987-12-01"), to_date = ToDateTimeUtc("1988-11-30")},
            new () {emp_no = 10004, salary = 42542, from_date = ToDateTimeUtc("1988-11-30"), to_date = ToDateTimeUtc("1989-11-30")},
            new () {emp_no = 10004, salary = 46065, from_date = ToDateTimeUtc("1989-11-30"), to_date = ToDateTimeUtc("1990-11-30")},
            new () {emp_no = 10004, salary = 48271, from_date = ToDateTimeUtc("1990-11-30"), to_date = ToDateTimeUtc("1991-11-30")},
            new () {emp_no = 10004, salary = 50594, from_date = ToDateTimeUtc("1991-11-30"), to_date = ToDateTimeUtc("1992-11-29")},
            new () {emp_no = 10004, salary = 52119, from_date = ToDateTimeUtc("1992-11-29"), to_date = ToDateTimeUtc("1993-11-29")},
            new () {emp_no = 10004, salary = 54693, from_date = ToDateTimeUtc("1993-11-29"), to_date = ToDateTimeUtc("1994-11-29")},
            new () {emp_no = 10004, salary = 58326, from_date = ToDateTimeUtc("1994-11-29"), to_date = ToDateTimeUtc("1995-11-29")},
            new () {emp_no = 10004, salary = 60770, from_date = ToDateTimeUtc("1995-11-29"), to_date = ToDateTimeUtc("1996-11-28")},
            new () {emp_no = 10004, salary = 62566, from_date = ToDateTimeUtc("1996-11-28"), to_date = ToDateTimeUtc("1997-11-28")},
            new () {emp_no = 10004, salary = 64340, from_date = ToDateTimeUtc("1997-11-28"), to_date = ToDateTimeUtc("1998-11-28")},
            new () {emp_no = 10004, salary = 67096, from_date = ToDateTimeUtc("1998-11-28"), to_date = ToDateTimeUtc("1999-11-28")},
            new () {emp_no = 10004, salary = 69722, from_date = ToDateTimeUtc("1999-11-28"), to_date = ToDateTimeUtc("2000-11-27")},
            new () {emp_no = 10004, salary = 70698, from_date = ToDateTimeUtc("2000-11-27"), to_date = ToDateTimeUtc("2001-11-27")},
            new () {emp_no = 10004, salary = 74057, from_date = ToDateTimeUtc("2001-11-27"), to_date = ToDateTimeUtc("9999-01-01")},
        ];

    private static DateTimeOffset ToDateTimeUtc(string date)
    {
        // var truncdate = TruncateTime(cd.LogTimestamp);
        var dateTimeParse = DateTime.Parse(date, new CultureInfo("de-DE"));
        var datetime = new DateTimeOffset(dateTimeParse.Year, dateTimeParse.Month, dateTimeParse.Day, 0, 0, 0, new());
        return datetime;
    }
}

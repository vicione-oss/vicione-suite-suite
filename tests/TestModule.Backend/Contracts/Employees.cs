using System.ComponentModel.DataAnnotations;

namespace TestModule.Backend.Contracts;

#pragma warning disable CS8618 // Ein Non-Nullable-Feld muss beim Beenden des Konstruktors einen Wert ungleich NULL enthalten. Erwägen Sie die Deklaration als Nullable.
public class Employees
{
    public enum Gender
    {
        M,
        F
    }

    [Key]
    public int emp_no { get; init; }
    public DateTimeOffset birth_date { get; init; }
    [MaxLength(14)]
    public string first_name { get; set; }
    [MaxLength(16)]
    public string last_name { get; set; }
    public Gender gender { get; init; }
    public DateTimeOffset hire_date { get; init; }
    public IList<DepartmentManager> dept_manager { get; init; }
    public IList<DepartmentEmployees> dept_empl { get; init; }
    public IList<Titles> titles { get; init; }
    public IList<Salaries> salaries { get; init; }
}
#pragma warning restore CS8618 // Ein Non-Nullable-Feld muss beim Beenden des Konstruktors einen Wert ungleich NULL enthalten. Erwägen Sie die Deklaration als Nullable.

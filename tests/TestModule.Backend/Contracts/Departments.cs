using System.ComponentModel.DataAnnotations;

namespace TestModule.Backend.Contracts;

#pragma warning disable CS8618 // Ein Non-Nullable-Feld muss beim Beenden des Konstruktors einen Wert ungleich NULL enthalten. Erwägen Sie die Deklaration als Nullable.
public class Departments
{
    [Key]
    [MaxLength(4)]
    public string dept_no { get; init; }
    [MaxLength(40)]
    public string dept_name { get; init; }
    public IList<DepartmentManager> dept_manager { get; init; }
    public IList<DepartmentEmployees> dept_empl { get; init; }
}
#pragma warning restore CS8618 // Ein Non-Nullable-Feld muss beim Beenden des Konstruktors einen Wert ungleich NULL enthalten. Erwägen Sie die Deklaration als Nullable.

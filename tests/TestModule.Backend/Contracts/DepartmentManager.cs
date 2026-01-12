using System.ComponentModel.DataAnnotations;

namespace TestModule.Backend.Contracts;

#pragma warning disable CS8618 // Ein Non-Nullable-Feld muss beim Beenden des Konstruktors einen Wert ungleich NULL enthalten. Erwägen Sie die Deklaration als Nullable.
public class DepartmentManager
{
    [Key]
    public int emp_no { get; set; }
    [Key]
    public string dept_no { get; set; }
    public DateTime from_date { get; set; }
    public DateTime to_date { get; set; }
    public Employees employee { get; set; }
    public Departments department { get; set; }
}
#pragma warning restore CS8618 // Ein Non-Nullable-Feld muss beim Beenden des Konstruktors einen Wert ungleich NULL enthalten. Erwägen Sie die Deklaration als Nullable.


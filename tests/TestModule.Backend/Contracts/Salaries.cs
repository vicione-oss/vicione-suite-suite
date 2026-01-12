using System.ComponentModel.DataAnnotations;

namespace TestModule.Backend.Contracts;

#pragma warning disable CS8618 // Ein Non-Nullable-Feld muss beim Beenden des Konstruktors einen Wert ungleich NULL enthalten. Erwägen Sie die Deklaration als Nullable.
public class Salaries
{
    [Key]
    public int emp_no { get; set; }
    public int salary { get; set; }
    [Key]
    public DateTime from_date { get; set; }
    public DateTime to_date { get; set; }
    public Employees employee { get; set; }
}
#pragma warning restore CS8618 // Ein Non-Nullable-Feld muss beim Beenden des Konstruktors einen Wert ungleich NULL enthalten. Erwägen Sie die Deklaration als Nullable.

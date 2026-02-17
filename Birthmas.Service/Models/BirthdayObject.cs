namespace Birthmas.Service.Models;

public sealed record BirthdayObject(string Name, DateOnly Birthday)
{
    public string StatusString => Birthday.ToString("MM/dd");
    public string TableString => GetTableString();

    private string GetTableString()
    {
        var dYear = Birthday.Year;
        var today = DateTime.Today;
        var offset = today.Year - dYear;
        return Birthday.AddYears(offset).ToString("dddd MMMM dd, yyyy");
    }
}
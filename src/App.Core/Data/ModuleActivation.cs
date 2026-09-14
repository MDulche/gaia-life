namespace App.Core.Data;

public class ModuleActivation
{
    public int Id { get; set; }

    public string ModuleKey { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

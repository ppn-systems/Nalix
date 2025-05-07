namespace Nalix.Graphics.Attributes;

[System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
public class NotLoadableAttribute : System.Attribute
{
    public string Reason { get; }

    public NotLoadableAttribute(string reason)
    {
        Reason = reason;
    }
}

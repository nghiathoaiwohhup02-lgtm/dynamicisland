namespace Autodesk.Revit.DB
{
    public class ElementSet { }
    public class CategorySet { }
}

namespace Autodesk.Revit.Attributes
{
    public enum TransactionMode { Manual = 1, ReadOnly = 2 }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class TransactionAttribute : System.Attribute
    {
        public TransactionAttribute(TransactionMode mode) { }
    }
}

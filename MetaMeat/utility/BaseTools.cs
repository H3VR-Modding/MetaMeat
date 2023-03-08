using System.Data;

namespace MetaMeat;

public abstract class BaseTools
{
    protected DataTable ObjectTable { get; }
    
    protected BaseTools(DataTable dataTable)
    {
        ObjectTable = dataTable;
    }

    public DataRow? GetRow(object pk) => ObjectTable.Rows.Find(pk);
}

using System.Collections.Generic;

namespace BehindTheBuzzword.CloudFormation.DTO
{
    public class DatabaseTableRequestData
    {
        public string Name { get; set; }
        public Dictionary<string, DatabaseTableColumn> Columns { get; set; }
    }

    public class DatabaseTableColumn
    {
        public string DataType { get; set; }
        public string PrimaryKey { get; set; }
        public string AutoIncrement { get; set; }

        public bool IsPrimaryKey => PrimaryKey.ToLower() == "true";
        public bool IsAutoIncrement => AutoIncrement.ToLower() == "true";
    }

    public class DatabaseTableResponseData
    {
        public string Name { get; set; }
    }
}

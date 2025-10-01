# xBase v0.1.0 Quick Reference Card

## Installation

### NuGet Packages
```bash
dotnet add package XBase.Core --version 1.0.0
dotnet add package XBase.Data --version 1.0.0
dotnet add package XBase.EFCore --version 1.0.0
dotnet tool install --global XBase.Tools --version 1.0.0
```

### CLI Tool Downloads
- **Windows**: [xbase-win-x64.zip](https://github.com/luckydizzier/xBase/releases/download/v0.1.0/xbase-win-x64.zip)
- **Linux**: [xbase-linux-x64.tar.gz](https://github.com/luckydizzier/xBase/releases/download/v0.1.0/xbase-linux-x64.tar.gz)
- **macOS**: [xbase-osx-arm64.tar.gz](https://github.com/luckydizzier/xBase/releases/download/v0.1.0/xbase-osx-arm64.tar.gz)

## Basic Usage

### ADO.NET Provider
```csharp
using XBase.Data;

var connStr = "Data Source=/path/to/database;Journaling=true";
using var connection = new XBaseConnection(connStr);
connection.Open();

using var command = connection.CreateCommand();
command.CommandText = "SELECT * FROM CUSTOMERS WHERE CITY = @city";
command.Parameters.Add(new XBaseParameter("@city", "Seattle"));

using var reader = command.ExecuteReader();
while (reader.Read())
{
    Console.WriteLine($"{reader["NAME"]} - {reader["EMAIL"]}");
}
```

### EF Core Provider
```csharp
using Microsoft.EntityFrameworkCore;
using XBase.EFCore;

public class MyDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseXBase("Data Source=/path/to/database");
    }

    public DbSet<Customer> Customers { get; set; }
}

// Usage
using var context = new MyDbContext();
var seattleCustomers = context.Customers
    .Where(c => c.City == "Seattle")
    .ToList();
```

### CLI Tool Commands
```bash
# Display table metadata
xbase dbfinfo /path/to/customers.dbf

# Export to CSV
xbase dbfdump /path/to/customers.dbf --format csv --output data.csv

# Compact deleted records
xbase dbfpack /path/to/customers.dbf

# Rebuild indexes
xbase dbfreindex /path/to/database customers

# Convert code page
xbase dbfconvert /path/to/source.dbf /path/to/output.dbf --codepage cp850

# Online DDL operations
xbase ddl apply /path/to/database customers --ddl schema.ddl
xbase ddl checkpoint /path/to/database customers
xbase ddl pack /path/to/database customers
xbase ddl reindex /path/to/database customers
```

## Supported Formats

| Format | Version | Status |
|--------|---------|--------|
| DBF | dBASE III+ (0x03) | ✅ Full support |
| DBF | dBASE IV (0x04, 0x8B) | ✅ Full support |
| DBT | dBASE III memo | ✅ Full support |
| DBT | dBASE IV memo | ✅ Full support |
| NTX | Clipper index | ✅ Navigation supported |
| MDX | dBASE IV compound | ✅ Navigation supported |
| FPT | FoxPro memo | 🔜 Phase B |
| CDX | FoxPro compound | 🔜 Phase B |

## Configuration Options

### Connection String Properties
| Property | Default | Description |
|----------|---------|-------------|
| `Data Source` | (required) | Path to database directory or DBF file |
| `Journaling` | `false` | Enable WAL journaling for transactions |
| `Lock Mode` | `File` | Locking mode: `File`, `Record`, `None` |
| `Code Page` | `CP437` | Default character encoding |
| `Read Only` | `false` | Open in read-only mode |

### Example Connection Strings
```
Data Source=C:\databases\mydb
Data Source=/var/lib/xbase/mydb;Journaling=true;Lock Mode=Record
Data Source=/path/to/table.dbf;Read Only=true;Code Page=CP850
```

## Documentation Links

- **Full Release Notes**: [RELEASE-NOTES-v0.1.0.md](RELEASE-NOTES-v0.1.0.md)
- **Release Process**: [RELEASE-PROCESS-GUIDE.md](RELEASE-PROCESS-GUIDE.md)
- **Architecture**: [../../architecture.md](../../architecture.md)
- **Requirements**: [../../requirements.md](../../requirements.md)
- **Transactions**: [../../TRANSACTIONS.md](../../TRANSACTIONS.md)
- **Code Pages**: [../../CODEPAGES.md](../../CODEPAGES.md)
- **Indexes**: [../../INDEXES.md](../../INDEXES.md)
- **Configuration**: [../configuration.md](../configuration.md)
- **Cookbooks**: [../cookbooks/](../cookbooks/)

## Build Information

- **Release Date**: 2025-10-01
- **Target Framework**: .NET 8.0 LTS
- **Build Configuration**: Release
- **Tests Passed**: 33/33 (100%)
- **Code Coverage**: Available in test results

## Checksums (SHA256)

```
5208a3c4cf8f8de053046039502b4a8953aaf9a28d7330c0f3ad3e9671384c36  xbase-win-x64.zip
32b16665d90e68049760499ba6e338ead6ce2596714993489f1450a78b536693  xbase-linux-x64.tar.gz
98e7f35d94ce46d9c5a7787500349fcfd22490101041bfa346de18ff64fefb5a  xbase-osx-arm64.tar.gz
```

## Support & Contributing

- **GitHub**: https://github.com/luckydizzier/xBase
- **Issues**: https://github.com/luckydizzier/xBase/issues
- **License**: See [LICENSE](../../LICENSE)

---

**Version**: 0.1.0 (Phase A GA)  
**Last Updated**: 2025-10-01

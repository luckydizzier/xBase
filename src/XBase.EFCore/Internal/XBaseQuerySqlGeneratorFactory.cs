using Microsoft.EntityFrameworkCore.Query;

namespace XBase.EFCore.Internal;

internal sealed class XBaseQuerySqlGeneratorFactory : IQuerySqlGeneratorFactory
{
  private readonly QuerySqlGeneratorDependencies _dependencies;

  public XBaseQuerySqlGeneratorFactory(QuerySqlGeneratorDependencies dependencies)
  {
    _dependencies = dependencies;
  }

  public QuerySqlGenerator Create()
  {
    return new QuerySqlGenerator(_dependencies);
  }
}

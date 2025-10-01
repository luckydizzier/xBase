using System;
using Microsoft.EntityFrameworkCore.Update;

namespace XBase.EFCore.Internal;

internal sealed class NoOpModificationCommandBatchFactory : IModificationCommandBatchFactory
{
  public ModificationCommandBatch Create()
  {
    throw new NotSupportedException("Data modifications are not supported by the XBase provider.");
  }
}

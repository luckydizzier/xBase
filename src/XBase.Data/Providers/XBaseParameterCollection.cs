using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;

namespace XBase.Data.Providers;

public sealed class XBaseParameterCollection : DbParameterCollection
{
  private readonly List<DbParameter> _parameters = new();

  public override int Count => _parameters.Count;

  public override object SyncRoot => ((ICollection)_parameters).SyncRoot;

  public override int Add(object value)
  {
    if (value is not DbParameter parameter)
    {
      throw new System.ArgumentException("Expected DbParameter", nameof(value));
    }

    _parameters.Add(parameter);
    return _parameters.Count - 1;
  }

  public override void AddRange(Array values)
  {
    foreach (object value in values)
    {
      Add(value);
    }
  }

  public override void Clear()
  {
    _parameters.Clear();
  }

  public override bool Contains(object value)
  {
    return _parameters.Contains((DbParameter)value);
  }

  public override bool Contains(string value)
  {
    return IndexOf(value) >= 0;
  }

  public override void CopyTo(Array array, int index)
  {
    ((ICollection)_parameters).CopyTo(array, index);
  }

  public override IEnumerator GetEnumerator()
  {
    return _parameters.GetEnumerator();
  }

  protected override DbParameter GetParameter(int index)
  {
    return _parameters[index];
  }

  protected override DbParameter GetParameter(string parameterName)
  {
    int index = IndexOf(parameterName);
    return _parameters[index];
  }

  public override int IndexOf(object value)
  {
    return _parameters.IndexOf((DbParameter)value);
  }

  public override int IndexOf(string parameterName)
  {
    for (int i = 0; i < _parameters.Count; i++)
    {
      if (string.Equals(_parameters[i].ParameterName, parameterName, System.StringComparison.Ordinal))
      {
        return i;
      }
    }

    return -1;
  }

  public override void Insert(int index, object value)
  {
    if (value is not DbParameter parameter)
    {
      throw new System.ArgumentException("Expected DbParameter", nameof(value));
    }

    _parameters.Insert(index, parameter);
  }

  public override void Remove(object value)
  {
    _parameters.Remove((DbParameter)value);
  }

  public override void RemoveAt(int index)
  {
    _parameters.RemoveAt(index);
  }

  public override void RemoveAt(string parameterName)
  {
    int index = IndexOf(parameterName);
    if (index >= 0)
    {
      RemoveAt(index);
    }
  }

  protected override void SetParameter(int index, DbParameter value)
  {
    _parameters[index] = value;
  }

  protected override void SetParameter(string parameterName, DbParameter value)
  {
    int index = IndexOf(parameterName);
    if (index >= 0)
    {
      _parameters[index] = value;
    }
    else
    {
      _parameters.Add(value);
    }
  }
}

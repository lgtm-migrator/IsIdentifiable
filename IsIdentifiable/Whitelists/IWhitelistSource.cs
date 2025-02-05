﻿using System;
using System.Collections.Generic;

namespace IsIdentifiable.Allowlists;

/// <summary>
/// Interface for classes that produce a list of values which when
/// detected as 'identifiable information' should infact instead
/// be ignored.
/// </summary>
public interface IAllowlistSource : IDisposable
{
    /// <summary>
    /// Return all unique strings which should be ignored.  These strings should be trimmed.  Case is not relevant.
    /// </summary>
    /// <returns></returns>
    IEnumerable<string> GetAllowlist();
}
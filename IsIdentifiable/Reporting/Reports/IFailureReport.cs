﻿
using IsIdentifiable.Options;

namespace IsIdentifiable.Reporting.Reports;

/// <summary>
/// Interface for classes which aggregate or persist <see cref="Failure"/> objects
/// generated by IsIdentifiable e.g. calculating a percentage of failures per column
/// or just persisting all failures to disk
/// </summary>
public interface IFailureReport
{
    /// <summary>
    /// Set the destination for the report, based on the given options
    /// </summary>
    /// <param name="options"></param>
    void AddDestinations(IsIdentifiableBaseOptions options);

    /// <summary>
    /// Call to indicate that you have processed <paramref name="numberDone"/> since you last called this method
    /// </summary>
    /// <param name="numberDone">Number of rows done since the last call to this method</param>
    void DoneRows(int numberDone);

    /// <summary>
    /// Record a failure for a value on a row.  This can be called multiple times per row.  
    /// </summary>
    /// <param name="failure"></param>
    void Add(Failure failure);

    /// <summary>
    /// Finish the report and write it to the destination(s)
    /// </summary>
    void CloseReport();
}
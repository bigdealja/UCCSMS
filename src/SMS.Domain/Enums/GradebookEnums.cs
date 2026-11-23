namespace SMS.Domain.Enums;

/// <summary>Which policy bucket an assessment component belongs to (drives the pass gate:
/// coursework >= 50% AND end-of-semester >= 50%).</summary>
public enum GradeCategory { Coursework, EndOfSemester }

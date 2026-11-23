namespace SMS.Domain.Enums;

public enum SpecialArrangementType { ExtraTime, SeparateRoom, Reader, Scribe, AssistiveTechnology, RestBreaks, LargePrint, Other }
public enum InvigilatorRole { Chief, Assistant }

/// <summary>Kinds of exam-timetable conflict the scheduler detects (EMS brief: clash checks).</summary>
public enum ClashType { StudentOverlap, RoomDoubleBooked, InvigilatorDoubleBooked, CapacityExceeded, TooManyExamsPerDay }
public enum ClashSeverity { Error, Warning }

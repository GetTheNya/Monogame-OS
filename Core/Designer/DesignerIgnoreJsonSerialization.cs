using System;

namespace TheGame.Core.Designer;

[AttributeUsage(AttributeTargets.Property)]
public class DesignerIgnoreJsonSerialization : Attribute { }

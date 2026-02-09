using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NACHOS;

public static class CodeEditorHelper {
    public static string GetEnclosingClassName(IReadOnlyList<string> lines, string fileName, int lineIdx) {
        // Walk upwards to find the nearest class/struct/record/interface definition
        int braceDepth = 0;
        var classRegex = new Regex(@"\b(?:class|struct|record|interface)\s+([A-Za-z0-9_]+)");
        
        for (int i = lineIdx; i >= 0; i--) {
            string line = lines[i];
            
            for (int charIdx = line.Length - 1; charIdx >= 0; charIdx--) {
                if (line[charIdx] == '}') braceDepth++;
                else if (line[charIdx] == '{') braceDepth--;
            }
            
            if (braceDepth < 0) {
                var match = classRegex.Match(line);
                if (match.Success) return match.Groups[1].Value;
                
                if (i > 0) {
                    match = classRegex.Match(lines[i-1]);
                    if (match.Success) return match.Groups[1].Value;
                }
                
                braceDepth = 0;
            }
        }
        
        return fileName.Replace(".cs", "");
    }

    public static string GetNamespace(IReadOnlyList<string> lines) {
        for (int i = 0; i < lines.Count; i++) {
            string l = lines[i].Trim();
            if (l.StartsWith("namespace ")) {
                return l.Substring(10).Trim().TrimEnd('{', ';', ' ');
            }
        }
        return "MyNamespace";
    }
}
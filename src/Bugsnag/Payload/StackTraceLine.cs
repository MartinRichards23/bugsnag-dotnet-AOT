using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace Bugsnag.Payload
{
  /// <summary>
  /// Represents a set of Bugsnag payload stacktrace lines that are generated from a single StackTrace provided
  /// by the runtime.
  /// </summary>
  public class StackTrace : IEnumerable<StackTraceLine>
  {
    private readonly System.Exception _originalException;

    public StackTrace(System.Exception exception)
    {
      _originalException = exception;
    }

    public IEnumerator<StackTraceLine> GetEnumerator()
    {
      if (_originalException == null)
      {
        yield break;
      }

      var exceptionStackTrace = true;
      var stackFrames = new System.Diagnostics.StackTrace(_originalException, true).GetFrames();

      if (stackFrames == null || stackFrames.Length == 0)
      {
        // this usually means that the exception has not been thrown so we need
        // to try and create a stack trace at the point that the notify call
        // was made.
        exceptionStackTrace = false;
        stackFrames = new System.Diagnostics.StackTrace(true).GetFrames();
      }

      if (stackFrames == null)
      {
        yield break;
      }

      bool seenBugsnagFrames = false;

      foreach (var frame in stackFrames)
      {
        var stackFrame = StackTraceLine.FromStackFrame(frame);

        if (!exceptionStackTrace)
        {
          // if the exception has not come from a stack trace then we need to
          // skip the frames that originate from inside the notifier code base
          var currentStackFrameIsNotify = !String.IsNullOrWhiteSpace(stackFrame.MethodName) && stackFrame.MethodName.StartsWith("Bugsnag.Client.Notify");
          seenBugsnagFrames = seenBugsnagFrames || currentStackFrameIsNotify;
          if (!seenBugsnagFrames || currentStackFrameIsNotify)
          {
            continue;
          }
        }

        yield return stackFrame;
      }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }

  /// <summary>
  /// Represents an individual stack trace line in the Bugsnag payload.
  /// </summary>
  public class StackTraceLine : Dictionary<string, object>
  {
    public static StackTraceLine FromStackFrame(StackFrame stackFrame)
    {
      var file = stackFrame.GetFileName();
      var lineNumber = stackFrame.GetFileLineNumber();
      string methodName;

#if NET9_0_OR_GREATER
      // If IsDynamicCodeSupported == true, we are not using AOT, so just default to the normal way to get method name
      if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
        methodName = GetMethodName(stackFrame);
      else
        methodName = GetMethodNameAOT(stackFrame);
#else
      methodName = GetMethodName(stackFrame);
#endif

      return new StackTraceLine(file, lineNumber, methodName, false);
    }

#if NET9_0_OR_GREATER
    /// <summary>
    /// Get the method name from the stack frame. This is AOT safe, only available in .NET9 or greater.
    /// </summary>
    private static string GetMethodNameAOT(StackFrame stackFrame)
    {
      // DiagnosticMethodInfo.Create is AOT safe, but only available int .NET 9+
      DiagnosticMethodInfo info = DiagnosticMethodInfo.Create(stackFrame);

      if(info == null)
        return null;
      else
        return $"{info.DeclaringTypeName}.{info.Name}";
    }
#endif

    /// <summary>
    /// Get the method name from the stack frame. This is not AOT safe.
    /// </summary>
    private static string GetMethodName(StackFrame stackFrame)
    {
      var method = stackFrame.GetMethod();
      return new Method(method).DisplayName();
    }

    public StackTraceLine(string file, int lineNumber, string methodName, bool inProject)
    {
      this.AddToPayload("file", file);
      this.AddToPayload("lineNumber", lineNumber);
      this.AddToPayload("method", methodName);
      this.AddToPayload("inProject", inProject);
    }

    public string FileName
    {
      get
      {
        return this.Get("file") as string;
      }
      set
      {
        this.AddToPayload("file", value);
      }
    }

    public string MethodName
    {
      get
      {
        return this.Get("method") as string;
      }
      set
      {
        this.AddToPayload("method", value);
      }
    }

    public bool InProject
    {
      get
      {
        return (bool)this.Get("inProject");
      }
      set
      {
        this.AddToPayload("inProject", value);
      }
    }
  }
}

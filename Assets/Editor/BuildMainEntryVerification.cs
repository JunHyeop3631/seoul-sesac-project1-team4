using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildMainEntryVerification
{
    [Serializable] sealed class Result
    {
        public bool passed; public string unity, target, backend, output, result, activeProfile, detail;
        public string[] scenes; public int errors, warnings; public long bytes; public double seconds;
    }
    static string Argument(string name)
    {
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,name);
        return index>=0 && index+1<args.Length?args[index+1]:null;
    }
    public static void Build()
    {
        var evidence=new Result{unity=Application.unityVersion,target="StandaloneWindows64"};
        try
        {
            var scenes=EditorBuildSettings.scenes.Where(scene=>scene.enabled).Select(scene=>scene.path).ToArray();evidence.scenes=scenes;
            if(scenes.Length<2 || scenes[0]!="Assets/Scenes/Title.unity" || scenes[1]!="Assets/Scenes/ThrowYut.unity")throw new InvalidOperationException("Global build must start Title then ThrowYut");
            var profileType=AppDomain.CurrentDomain.GetAssemblies().Select(assembly=>assembly.GetType("UnityEditor.Build.Profile.BuildProfile")).FirstOrDefault(type=>type!=null);
            var getProfile=profileType?.GetMethod("GetActiveBuildProfile",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
            var profile=getProfile?.Invoke(null,null) as UnityEngine.Object;evidence.activeProfile=profile==null?"Global scene list":profile.name;
            if(profile!=null)
            {
                var data=new SerializedObject(profile);var iterator=data.GetIterator();
                while(iterator.Next(true))if(iterator.propertyType==SerializedPropertyType.Boolean && iterator.name.ToLowerInvariant().Contains("overrideglobalscene") && iterator.boolValue)
                    throw new InvalidOperationException("Active profile overrides the global scene list: "+profile.name);
            }
            evidence.output=Argument("-verification-build-output");if(string.IsNullOrEmpty(evidence.output))throw new InvalidOperationException("Missing verification build output");
            if(!File.Exists("Assets/__Verification/MainEntryPlayerProbe.cs"))throw new InvalidOperationException("Install the isolated command-line probe before verification build");
            Directory.CreateDirectory(Path.GetDirectoryName(evidence.output));evidence.backend=PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone).ToString();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=evidence.output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            evidence.result=report.summary.result.ToString();evidence.passed=report.summary.result==BuildResult.Succeeded;
            evidence.errors=(int)report.summary.totalErrors;evidence.warnings=(int)report.summary.totalWarnings;evidence.bytes=(long)report.summary.totalSize;evidence.seconds=report.summary.totalTime.TotalSeconds;
            evidence.detail=string.Join("\n",report.steps.SelectMany(step=>step.messages).Where(message=>message.type==LogType.Error || message.type==LogType.Exception).Select(message=>message.content));
        }
        catch(Exception exception){evidence.passed=false;evidence.detail=exception.ToString();Debug.LogException(exception);}
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/BuildMainEntryBuild.json",JsonUtility.ToJson(evidence,true));EditorApplication.Exit(evidence.passed?0:1);
    }
}

using System;using System.Linq;using UnityEditor;using UnityEditor.PackageManager;using UnityEngine;
public static class HeadlessConnectivityCheck{
 public static void Run(){
  var forbidden=new[]{"com.dreamy.ui","com.dreamy.feature","com.unity.ugui"};
  var packages=UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
  foreach(var name in forbidden)if(packages.Any(p=>p.name==name))throw new Exception("Unexpected UI dependency: "+name);
  foreach(var a in AppDomain.CurrentDomain.GetAssemblies())if(a.GetName().Name=="Dreamy.Connectivity.UI")throw new Exception("Sample UI compiled without import");
  using(var service=new Dreamy.Connectivity.ConnectivityService(new Dreamy.Connectivity.UnityReachabilityGateway())){
   var check=service.CheckAsync(new Dreamy.Connectivity.ConnectivityCheckRequest("headless"),default(System.Threading.CancellationToken)).GetAwaiter().GetResult();
   Debug.Log("PASS_HEADLESS_CONNECTIVITY_NO_UI: "+check.Status);
  }
 }
}
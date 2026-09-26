using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NuclearOptionAMRAAM
{
    public sealed class MenuCaptureDriver : MonoBehaviour
    {
        internal void Schedule(Action action) { StartCoroutine(Capture(action)); }
        IEnumerator Capture(Action action)
        {
            yield return null; yield return null;
            try { action(); }
            catch(Exception error) { Debug.LogException(error); }
            Application.Quit();
        }
    }
    internal static class MenuSmokeCapture
    {
        internal static void Render(string output,StringBuilder report)
        {
            var prefab=Resources.FindObjectsOfTypeAll<WeaponSelector>().FirstOrDefault();
            if(prefab==null) throw new Exception("No native WeaponSelector available for rendered menu verification");
            var aircraft=Encyclopedia.i.aircraft.First(d=>d.unitName.Contains("Revoker"));
            var set=aircraft.unitPrefab.GetComponentInChildren<WeaponManager>(true).hardpointSets
                .First(s=>s.weaponOptions.Count(m=>MountOptions.IsAMRAAM(m) && m.ammo==1)>1);
            var root=new GameObject("AMRAAM native chooser capture",typeof(RectTransform),typeof(Canvas));
            var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.planeDistance=1;
            var camera=new GameObject("AMRAAM chooser camera").AddComponent<Camera>();
            camera.cullingMask=1<<30; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.12f,.17f,.20f);
            var rt=new RenderTexture(1000,1200,24); camera.targetTexture=rt; canvas.worldCamera=camera;
            var selector=Object.Instantiate(prefab,root.transform,false);
            selector.gameObject.SetActive(true);
            var rect=selector.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);
            rect.pivot=new Vector2(.5f,1); rect.anchoredPosition=new Vector2(0,-50); rect.sizeDelta=new Vector2(650,100);
            selector.Initialize(set,new NuclearOption.SavedMission.SavedLoadout.SelectedMount{Key=""});
            var dropdown=(TMP_Dropdown)AccessTools.Field(typeof(WeaponSelector),"dropdown").GetValue(selector);
            dropdown.gameObject.SetActive(true); dropdown.alphaFadeSpeed=0;
            // Capture is same-frame; Unity has not yet called this component's Start.
            AccessTools.Method(typeof(TMP_Dropdown),"Start").Invoke(dropdown,null);
            dropdown.itemText=dropdown.template.GetComponentInChildren<TMP_Text>(true);
            var labels=dropdown.options.Select(o=>o.text).ToArray();
            report.AppendLine("Rendered chooser options: "+string.Join(" | ",labels));
            File.WriteAllLines(Path.Combine(output,"chooser-options.txt"),labels);
            selector.SetValue(set.weaponOptions.Last(m=>MountOptions.IsAMRAAM(m) && m.ammo==1));
            if(!MountOptions.IsAMRAAM(selector.GetValue()) || selector.GetValue().ammo!=1) throw new Exception("Legacy mount selection failed");
            dropdown.SetValueWithoutNotify(Array.FindIndex(labels,s=>s=="AIM-120 AMRAAM"));
            Canvas.ForceUpdateCanvases(); dropdown.Show(); Canvas.ForceUpdateCanvases();
            foreach(var group in root.GetComponentsInChildren<CanvasGroup>(true)) group.alpha=1f;
            foreach(var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=30;
            root.AddComponent<MenuCaptureDriver>().Schedule(()=> {
            Canvas.ForceUpdateCanvases();
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true,true);
            File.WriteAllLines(Path.Combine(output,"chooser-text-debug.txt"),root.GetComponentsInChildren<TMP_Text>(true).Select(t=>$"{t.name} active={t.gameObject.activeInHierarchy}, enabled={t.enabled}, text={t.text}, vertices={t.textInfo.characterCount}, color={t.color}, rect={t.rectTransform.rect}, cull={t.canvasRenderer.cull}, pos={t.transform.position}"));
            foreach(var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=30;
            camera.Render(); var previous=RenderTexture.active; RenderTexture.active=rt;
            var image=new Texture2D(1000,1200,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1000,1200),0,0); image.Apply();
            File.WriteAllBytes(Path.Combine(output,"chooser-native.png"),image.EncodeToPNG()); RenderTexture.active=previous;
            Object.Destroy(root); camera.targetTexture=null; Object.Destroy(camera.gameObject); rt.Release(); Object.Destroy(rt); Object.Destroy(image);
            });
        }
    }
}

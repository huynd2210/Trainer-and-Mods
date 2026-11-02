using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SwampKeeperCoop.Protocol;
using Object=UnityEngine.Object;

namespace SwampKeeperCoop;

internal sealed class CoopHud
{
    internal static CoopHud? Instance;
    private GameObject? root;
    private TextMeshProUGUI? status,stats,skills,team;
    private readonly List<Image> icons=new();private TMP_FontAsset? font;
    private RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var go=new GameObject(name);var rt=go.AddComponent<RectTransform>();rt.SetParent(parent,false);
        rt.anchorMin=rt.anchorMax=rt.pivot=new(0,1);rt.anchoredPosition=new(x,-y);rt.sizeDelta=new(w,h);return rt;
    }
    private TextMeshProUGUI Text(Transform parent,string name,float x,float y,float w,float h,float size)
    {
        var t=Rect(name,parent,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.color=new(.88f,.94f,.84f,1);t.raycastTarget=false;t.enableWordWrapping=false;return t;
    }
    internal void Build()
    {
        var source=Object.FindObjectOfType<TextMeshProUGUI>();if(!source)return;font=source.font;
        root=new GameObject("CoopHud");Instance=this;var canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=5000;
        var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new(1280,800);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var top=Rect("Session",root.transform,16,16,430,92);var bg=top.gameObject.AddComponent<Image>();bg.color=new(.04f,.08f,.09f,.91f);bg.raycastTarget=false;
        status=Text(top,"Status",14,10,402,26,19);team=Text(top,"Team",14,41,402,44,16);
        var bottom=Rect("Player",root.transform,400,692,480,92);bg=bottom.gameObject.AddComponent<Image>();bg.color=new(.04f,.08f,.09f,.94f);bg.raycastTarget=false;
        stats=Text(bottom,"Arrows",14,10,452,28,21);skills=Text(bottom,"Skills",102,45,360,40,16);
        for(int i=0;i<2;i++){var img=Rect("Skill"+i,bottom,14+i*40,46,34,34).gameObject.AddComponent<Image>();img.preserveAspect=true;img.raycastTarget=false;icons.Add(img);}
    }
    internal void Apply(WorldState world)
    {
        if(!status){Build();if(!status)return;}
        Visible(!SessionMenu.Open);
        var local=world.Actors.Single(a=>a.Id==1);var host=world.Actors.Single(a=>a.Id==0);
        status!.text=!host.Alive && !local.Alive?"TEAM DOWN  ·  HOST CAN RESTART":"ONLINE CO-OP  ·  PLAYER 2";
        team!.text="Host "+(host.Alive?"alive":"down")+"  |  You "+(local.Alive?"alive":"down")+"\nStage "+(world.Stage+1)+"  ·  "+Mathf.FloorToInt(world.Timer)+" sec";
        stats!.text="ARROWS "+local.Arrows+" / "+local.Capacity+"     KILLS "+local.Kills+"     "+(local.HasTail?"TAIL READY":"TAIL LOST");
        skills!.text=string.Join("     ",local.Charges.Select((n,i)=>"Charge "+n+" / "+SkillManager.sm.GetSkill(local.Skills[i]).GetCost()));
        for(int i=0;i<icons.Count;i++){var skill=i<local.Skills.Length?SkillManager.sm.GetSkill(local.Skills[i]):null;icons[i].enabled=skill; if(skill)icons[i].sprite=skill!.icon;}
    }
    internal void Visible(bool value){if(root)root!.SetActive(value);}
}

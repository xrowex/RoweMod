using UnityEditor;
using UnityEngine;

public sealed partial class RoweIKPoseEditor
{
    GUIStyle roweCard, rowePrimary, roweChoice, roweSelected, roweTitle, roweCaption,roweControl;
    Texture2D rowePanelTexture,roweHoverTexture,roweOrangeTexture;
    void EnsureRoweTheme()
    {
        // Unity hot reload can restore old GUIStyle fields but not newly added ones.
        // Treat the theme as one cache; never hand GUILayout a partially restored style.
        if(rowePrimary!=null&&roweControl!=null&&roweCard!=null&&roweChoice!=null&&roweSelected!=null&&rowePanelTexture&&roweHoverTexture&&roweOrangeTexture)return;
        ReleaseRoweTheme();
        Texture2D Solid(Color color) {var t=new Texture2D(1,1){hideFlags=HideFlags.HideAndDontSave};t.SetPixel(0,0,color);t.Apply();return t;}
        rowePanelTexture=Solid(new Color(.085f,.105f,.14f));roweHoverTexture=Solid(new Color(.15f,.18f,.23f));roweOrangeTexture=Solid(new Color(1f,.48f,.2f));
        roweCard=new GUIStyle(GUI.skin.box){padding=new RectOffset(14,14,12,12)};roweCard.normal.background=rowePanelTexture;
        roweChoice=new GUIStyle(GUI.skin.button){fontSize=13,alignment=TextAnchor.MiddleLeft,wordWrap=true,padding=new RectOffset(16,16,12,12),margin=new RectOffset(0,0,4,4)};
        roweChoice.normal.background=rowePanelTexture;roweChoice.normal.textColor=new Color(.92f,.94f,.97f);
        roweChoice.hover.background=roweHoverTexture;roweChoice.hover.textColor=Color.white;
        roweChoice.active.background=roweOrangeTexture;roweChoice.active.textColor=new Color(.06f,.07f,.09f);
        roweSelected=new GUIStyle(roweChoice);roweSelected.normal.background=roweOrangeTexture;roweSelected.normal.textColor=new Color(.06f,.07f,.09f);
        rowePrimary=new GUIStyle(roweSelected){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
        roweControl=new GUIStyle(roweChoice){fontSize=12,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(5,5,2,2),margin=new RectOffset(2,2,2,2)};
        roweControl.normal.background=roweHoverTexture;roweControl.onNormal.background=roweOrangeTexture;roweControl.onNormal.textColor=new Color(.06f,.07f,.09f);
        roweControl.onHover.background=roweOrangeTexture;roweControl.onHover.textColor=new Color(.06f,.07f,.09f);roweControl.onActive.background=roweOrangeTexture;
        roweTitle=new GUIStyle(EditorStyles.boldLabel){fontSize=20,normal={textColor=Color.white}};
        roweCaption=new GUIStyle(EditorStyles.wordWrappedLabel){fontSize=12,normal={textColor=new Color(.67f,.74f,.83f)}};
    }
    void ReleaseRoweTheme()
    {
        if(rowePanelTexture)DestroyImmediate(rowePanelTexture);if(roweHoverTexture)DestroyImmediate(roweHoverTexture);if(roweOrangeTexture)DestroyImmediate(roweOrangeTexture);
        rowePrimary=null;roweChoice=null;roweSelected=null;roweCard=null;roweControl=null;roweTitle=null;roweCaption=null;
    }
    void DrawRoweHeader()
    {
        EnsureRoweTheme();
        using(new EditorGUILayout.VerticalScope(roweCard))
        {
            EditorGUILayout.LabelField("ROWE MODS",new GUIStyle(EditorStyles.boldLabel){normal={textColor=new Color(1f,.48f,.2f)}});
            EditorGUILayout.LabelField("Animation Studio",roweTitle,GUILayout.Height(27));
            EditorGUILayout.LabelField("Create it. Pose it. Ride it.",roweCaption);
        }
    }
    bool RowePrimaryButton(string label) {EnsureRoweTheme();return GUILayout.Button(label,rowePrimary,GUILayout.MinHeight(38));}
    GUIStyle RoweControlStyle { get {EnsureRoweTheme();return roweControl;} }
    int RoweControlTabs(int current,params string[] labels)
    {
        using(new EditorGUILayout.HorizontalScope())
            for(int i=0;i<labels.Length;i++)if(GUILayout.Toggle(i==current,labels[i],RoweControlStyle,GUILayout.Height(30)))current=i;
        return current;
    }
    void DrawWizardNavigation(int[] steps,string[] titles)
    {
        int index=System.Array.IndexOf(steps,studioStep);
        EditorGUILayout.Space(12);
        using(new EditorGUILayout.HorizontalScope())
        {
            using(new EditorGUI.DisabledScope(index<=0))
                if(GUILayout.Button("← Back",GUILayout.Height(38),GUILayout.Width(95)))GoToWizardStep(steps[index-1]);
            if(index<steps.Length-1)
            {
                string problem=studioStep==2?RoweTrickStyles.Readiness(recipe):null;
                using(new EditorGUI.DisabledScope(problem!=null))
                    if(RowePrimaryButton("Next: "+titles[steps[index+1]]+" →"))GoToWizardStep(steps[index+1]);
            }
        }
    }
    void GoToWizardStep(int next)
    {
        Run(()=>{Commit();StopWithoutCommit();studioStep=next;studioScroll=Vector2.zero;
            if(next<2 || (next==2&&!IsLoopStyle&&recipe.tweakPose&&recipe.useCustomTweak))BeginStudioPose(next);});
    }
}

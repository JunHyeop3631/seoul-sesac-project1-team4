using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class EnemyKillRewards : MonoBehaviour
{
    [SerializeField] private WaveManager waves;
    [SerializeField] private TurnController turn;
    [SerializeField] private StageCurrency currency;
    [SerializeField] private Sprite coin;
    [SerializeField] private Sprite[] digits;
    [SerializeField] private Font font;
    [SerializeField, Min(0)] private int normalReward = 2, fastReward = 2, bossReward = 100;
    [SerializeField, Min(.1f)] private float duration = .8f;
    [SerializeField, Min(.1f)] private float popupScale = 1.6f;
    private readonly List<Popup> popups = new();
    private RectTransform container;
    private Canvas canvas;
    private sealed class Popup
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 origin;
        public float age;
    }
    private void Awake(){container=(RectTransform)transform;canvas=GetComponentInParent<Canvas>();}
    private void OnEnable()
    {
        if(waves!=null)waves.EnemyDefeated+=OnKill;
        if(turn!=null){turn.BattleLost+=Clear;turn.BattleWon+=Clear;}
    }
    private void OnDisable()
    {
        if(waves!=null)waves.EnemyDefeated-=OnKill;
        if(turn!=null){turn.BattleLost-=Clear;turn.BattleWon-=Clear;}
        Clear();
    }
    private void OnKill(Enemy enemy)
    {
        if(!isActiveAndEnabled||enemy==null||currency==null||turn==null||waves==null||
            !waves.IsRunning||turn.RemainingLives<=0||turn.IsBattleWon||turn.IsBattleLost)return;
        int amount=enemy is BossEnemy?bossReward:enemy is NormalEnemy?normalReward:enemy is FastEnemy?fastReward:0;
        // Wallet listeners may return/reuse the enemy; capture the kill position first.
        var visual=enemy.GetComponentInChildren<SpriteRenderer>();
        Vector3 position=visual!=null?new Vector3(visual.bounds.center.x,visual.bounds.max.y+.35f,0):enemy.transform.position+Vector3.up;
        int credited=currency.Grant(amount);
        if(credited<=0||turn.RemainingLives<=0||turn.IsBattleWon||turn.IsBattleLost||!waves.IsRunning)return;
        Show(position,credited);
    }
    private void Show(Vector3 world,int amount)
    {
        var camera=Camera.main;if(camera==null||canvas==null||coin==null)return;
        var uiCamera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(container,camera.WorldToScreenPoint(world),uiCamera,out Vector2 point))return;
        string value=amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var go=new GameObject("Kill Reward +"+value,typeof(RectTransform),typeof(CanvasGroup));go.layer=gameObject.layer;go.transform.SetParent(transform,false);
        var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(32+value.Length*11,22);rect.anchoredPosition=point;
        rect.localScale=Vector3.one*popupScale;
        var group=go.GetComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
        float left=-rect.sizeDelta.x*.5f;
        Image(go.transform,"Coin",coin,new Vector2(left+9,0),new Vector2(18,18));
        var plus=new GameObject("Plus",typeof(RectTransform),typeof(Text),typeof(Shadow));plus.layer=gameObject.layer;plus.transform.SetParent(go.transform,false);
        var text=plus.GetComponent<Text>();text.text="+";text.font=font;text.fontSize=20;text.fontStyle=FontStyle.Bold;text.alignment=TextAnchor.MiddleCenter;text.color=new Color(1,.88f,.57f);text.raycastTarget=false;
        text.rectTransform.sizeDelta=new Vector2(13,23);text.rectTransform.anchoredPosition=new Vector2(left+26,0);
        var shadow=plus.GetComponent<Shadow>();shadow.effectColor=new Color(.2f,.08f,.025f,1);shadow.effectDistance=new Vector2(1,-1);
        for(int i=0;i<value.Length;i++)Image(go.transform,"Digit "+i,digits[value[i]-'0'],new Vector2(left+37+i*11,0),new Vector2(12,19));
        popups.Add(new Popup{rect=rect,group=group,origin=point});
    }
    private static void Image(Transform parent,string name,Sprite sprite,Vector2 pos,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>();image.sprite=sprite;image.raycastTarget=false;image.preserveAspect=true;
        image.rectTransform.sizeDelta=size;image.rectTransform.anchoredPosition=pos;
    }
    private void Update()
    {
        for(int i=popups.Count-1;i>=0;i--)
        {
            var popup=popups[i];popup.age+=Time.deltaTime;
            float t=Mathf.Clamp01(popup.age/duration);
            popup.rect.anchoredPosition=popup.origin+Vector2.up*(24*t);
            popup.group.alpha=1-Mathf.InverseLerp(.45f,1,t);
            if(t>=1){Destroy(popup.rect.gameObject);popups.RemoveAt(i);}
        }
    }
    private void Clear(){foreach(var popup in popups)if(popup.rect!=null)Destroy(popup.rect.gameObject);popups.Clear();}
}

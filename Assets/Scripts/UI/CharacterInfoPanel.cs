using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public sealed class CharacterInfoPanel : MonoBehaviour
{
    [SerializeField] private TurnController turnController;
    private enum UnitKind
    {
        DoTaoist,
        GaeWarrior,
        GeolSpearman,
        YutMaceman,
        MoArcher
    }

    [Serializable]
    private sealed class Profile
    {
        public UnitKind unitKind = UnitKind.DoTaoist;
        public Sprite nameSprite = null;
        public Sprite idleLeft = null;
        public Sprite evolvedIdleLeft = null;
    }

    [SerializeField] private Image characterName;
    [SerializeField] private Image characterPortrait;
    [SerializeField] private GameObject firstStar;
    [SerializeField] private GameObject secondStar;
    [SerializeField] private Text attackValue;
    [SerializeField] private Text attackSpeedValue;
    [SerializeField] private Profile[] profiles = Array.Empty<Profile>();

    public void Show(Ally ally)
    {
        if (turnController == null || !turnController.CanUsePreparationInput ||
            ally == null || !ally.isActiveAndEnabled || !TryGetKind(ally, out UnitKind kind))
        {
            gameObject.SetActive(false);
            return;
        }

        Profile profile = Array.Find(profiles, entry => entry != null && entry.unitKind == kind);
        if (profile == null)
        {
            gameObject.SetActive(false);
            return;
        }

        bool evolved = ally.EvolutionStage >= 2;
        if (characterPortrait != null)
        {
            characterPortrait.sprite = evolved ? profile.evolvedIdleLeft : profile.idleLeft;
            characterPortrait.enabled = characterPortrait.sprite != null;
            characterPortrait.preserveAspect = true;
        }
        if (characterName != null)
        {
            characterName.sprite = profile.nameSprite;
            characterName.enabled = profile.nameSprite != null;
        }
        if (firstStar != null)
            firstStar.SetActive(true);
        if (secondStar != null)
            secondStar.SetActive(evolved);
        if (attackValue != null)
        {
            attackValue.text = ally.AttackDamage.ToString("0.#", CultureInfo.InvariantCulture);
        }
        if (attackSpeedValue != null)
        {
            attackSpeedValue.text = ally.AttacksPerSecond.ToString("0.0", CultureInfo.InvariantCulture) + "회/초";
        }

        gameObject.SetActive(true);
    }

    private void OnEnable()
    {
        if (turnController == null || !turnController.CanUsePreparationInput)
            gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (turnController == null || !turnController.CanUsePreparationInput)
            gameObject.SetActive(false);
    }

    private static bool TryGetKind(Ally ally, out UnitKind kind)
    {
        switch (ally)
        {
            case Do_Taoist _: kind = UnitKind.DoTaoist; return true;
            case Gae_Warrior _: kind = UnitKind.GaeWarrior; return true;
            case Geol_Spearman _: kind = UnitKind.GeolSpearman; return true;
            case Yut_Maceman _: kind = UnitKind.YutMaceman; return true;
            case Mo_Archer _: kind = UnitKind.MoArcher; return true;
            default: kind = default; return false;
        }
    }
}

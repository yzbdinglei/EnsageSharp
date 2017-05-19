using System.Linq;
using System.Threading.Tasks;
using ArcAnnihilation.Utils;
using Ensage;
using Ensage.Common.Enums;
using Ensage.Common.Extensions;
using Ensage.Common.Extensions.SharpDX;
using Ensage.Common.Objects.UtilityObjects;
using Ensage.Common.Threading;

namespace ArcAnnihilation.Units.behaviour.Items
{
    internal class CanUseItems : ICanUseItems
    {
        private static readonly Sleeper GlobalHexSleeper = new Sleeper();
        private static readonly Sleeper GlobalOrchidSleeper = new Sleeper();
        private readonly Sleeper _afterBlink;
        private readonly Sleeper _afterInvis;
        private readonly MultiSleeper _multiSleeper;

        public CanUseItems()
        {
            _multiSleeper = new MultiSleeper();
            _afterBlink = new Sleeper();
            _afterInvis = new Sleeper();
        }

        public async Task<bool> UseItems(UnitBase unitBase)
        {
            if (!unitBase.Hero.CanUseItems())
                return true;
            foreach (var ability in unitBase.GetItems())
            {
                if (!ability.CanBeCasted(Core.Target))
                    continue;
                var canHit = (ability.GetItemId() == ItemId.item_blink
                                 ? unitBase.Hero.Distance2D(Core.Target) - MenuManager.GetBlinkExtraRange <
                                   ability.TravelDistance()
                                 : ability.CanHit(Core.Target)) || _afterBlink.Sleeping;
                var isNoTarget = ability.IsAbilityBehavior(AbilityBehavior.NoTarget) &&
                                 (unitBase.Hero.Distance2D(Core.Target) <= 750 || ability.IsInvis());
                if (!canHit && !isNoTarget)
                {
                    Printer.Both($"{ability.Name} cant hit target!");
                    continue;
                }
                /*if (isNoTarget && unitBase.Hero.Distance2D(Core.Target)>=800)
                    continue;*/
                // if (canHit || _afterBlink)
                if (_afterInvis.Sleeping || unitBase.Hero.IsInvisible())
                {
                    Printer.Both($"{ability.Name} cant cast, cuz under invis!");
                    continue;
                }
                var delayTime = 0;
                if (ability.IsAbilityBehavior(AbilityBehavior.NoTarget))
                {
                    if (ability.IsInvis())
                        _afterInvis.Sleep(500);
                    ability.UseAbility();
                }
                else if (ability.IsAbilityBehavior(AbilityBehavior.UnitTarget))
                {
                    if (ability.GetItemId() == ItemId.item_hurricane_pike)
                    {
                        ability.UseAbility(unitBase.Hero);
                    }
                    else if (ability.TargetTeamType == TargetTeamType.Enemy || ability.TargetTeamType == TargetTeamType.All ||
                             ability.TargetTeamType == TargetTeamType.Custom || ability.TargetTeamType == (TargetTeamType) 7)
                    {
                        var isDisable = ability.IsDisable();
                        if (Core.Target.IsLinkensProtected() && isDisable)
                            continue;
                        if (ability.GetItemId() == ItemId.item_ethereal_blade &&
                            unitBase.GetItems()
                                .Any(
                                    x =>
                                        (x.GetItemId() == ItemId.item_dagon || x.GetItemId() == ItemId.item_dagon_2 ||
                                         x.GetItemId() == ItemId.item_dagon_3 || x.GetItemId() == ItemId.item_dagon_4 ||
                                         x.GetItemId() == ItemId.item_dagon_5) && x.CanBeCasted()))
                        {
                            ability.UseAbility(Core.Target);
                            await Core.Target.WaitGainModifierAsync("modifier_item_ethereal_blade_ethereal", 2,
                                Core.ComboToken.Token);
                        }
                        else
                        {
                            var slarkMod =
                                Core.Target.HasModifiers(
                                    new[] {"modifier_slark_dark_pact", "modifier_slark_dark_pact_pulses"}, false);
                            if (slarkMod && isDisable)
                                continue;
                            if (ability.GetItemId() == ItemId.item_sheepstick && GlobalHexSleeper.Sleeping)
                                continue;
                            if ((ability.GetItemId() == ItemId.item_orchid ||
                                 ability.GetItemId() == ItemId.item_bloodthorn) && GlobalOrchidSleeper.Sleeping)
                                continue;
                            if (isDisable)
                            {
                                if (Core.Target.IsUnitState(UnitState.Stunned) ||
                                    Core.Target.IsUnitState(UnitState.Hexed))
                                {
                                    var time = Ensage.Common.Utils.DisableDuration(Core.Target);
                                    if (time >= 0.35f)
                                        continue;
                                }
                                ability.UseAbility(Core.Target);
                                GlobalHexSleeper.Sleep(800);
                            }
                            else
                            {
                                if (ability.IsSilence())
                                {
                                    if (Core.Target.IsSilenced())
                                        continue;
                                    GlobalOrchidSleeper.Sleep(800);
                                }
                                ability.UseAbility(Core.Target);
                            }
                        }
                    }
                    else
                    {
                        ability.UseAbility(unitBase.Hero);
                    }
                }
                else
                {
                    if (ability.GetItemId() == ItemId.item_blink)
                    {
                        _afterBlink.Sleep(500);
                        var pos = (Core.Target.NetworkPosition - unitBase.Hero.NetworkPosition).Normalized();
                        var firstDist = Core.Target.NetworkPosition.Distance2D(unitBase.Hero.NetworkPosition);
                        Printer.Both($"FirstDist: {firstDist}");
                        pos *= firstDist <= ability.TravelDistance() ? 50 : MenuManager.GetBlinkExtraRange;
                        pos = Core.Target.NetworkPosition - pos;
                        if (pos.Distance2D(unitBase.Hero.NetworkPosition) < MenuManager.GetBlinkMinRange)
                            continue;
                        ability.UseAbility(pos);
                    }
                    else
                    {
                        ability.UseAbility(Core.Target.Position);
                    }
                }
                delayTime = ability.GetAbilityDelay();
                Printer.Both(
                    $"[{unitBase}][item] {ability.Name} ({delayTime}) [After Blink: {_afterBlink.Sleeping}] [{ability.TravelDistance()}]");
                await Await.Delay(delayTime, Core.ComboToken.Token);
            }
            return !unitBase.GetItems().Any(x => x.CanBeCasted() || x.GetItemId() == ItemId.item_blink);
        }
    }
}
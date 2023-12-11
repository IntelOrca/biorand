using System;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Events.Plots
{
    internal class EnemyFromDarkPlot : IPlot
    {
        public CsPlot BuildPlot(PlotBuilder builder)
        {
            var min = Math.Max(1, builder.MaximumEnemyCount - 2);
            var max = builder.MaximumEnemyCount + 2;
            var enemies = builder.AllocateEnemies(min: min, max: max);
            var plotFlag = builder.AllocateGlobalFlag();

            var trigger = new SbProcedure(
                builder.CreateTrigger(),
                new SbSetFlag(plotFlag),
                new SbLockPlot(
                    new SbLockControls(
                        CreateFlicker(
                            new SbSleep(30),
                            new SbCommentNode($"[action] spawn {enemies.Length} enemies",
                                enemies.Select(x =>
                                    new SbContainerNode(
                                        new SbMoveEntity(x, x.DefaultPosition),
                                        new SbSetEntityEnabled(x, true))).ToArray())))));

            var init = new SbProcedure(
                new SbCommentNode($"[plot] {enemies.Length} enemies from darkness {{ flag {plotFlag.Flag} }}",
                    new SbIf(plotFlag, false,
                        new SbContainerNode(
                            enemies.Select(x => new SbEnemy(x, REPosition.OutOfBounds, enabled: false)).ToArray()),
                        new SbFork(trigger))
                    .Else(
                        new SbContainerNode(
                            enemies.Select(x => new SbEnemy(x)).ToArray()))));

            return new CsPlot(init, endOfScript: true);
        }

        private static SbNode CreateFlicker(params SbNode[] children)
        {
            var sbb = new SbNodeBuilder();
            sbb.Append(new SbSetFade(0, 2, 7, 0, 0));
            for (var i = 0; i < 5; i++)
            {
                sbb.Append(new SbAdjustFade(0, 0, (byte)((i & 1) == 0 ? 0 : 127)));
                sbb.Append(new SbSleep(1));
            }
            sbb.Append(new SbAdjustFade(0, 0, 127));
            sbb.Reparent(x => new SbCommentNode("[action] flicker lights", x));

            sbb.Append(children);

            sbb.Append(new SbAdjustFade(0, 0, 0));
            sbb.Append(new SbSleep(1));
            sbb.Append(new SbSetFade(0, 2, 7, 255, 127));
            sbb.Append(new SbSleep(1));
            return sbb.Build();
        }
    }
}

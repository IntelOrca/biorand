using System.Collections.Generic;
using IntelOrca.Biohazard.BioRand.Events;
using IntelOrca.Biohazard.BioRand.RE2;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal class Storyboard
    {
        private Queue<byte> _availableAots = new Queue<byte>();
        private Queue<byte> _availableEnemies = new Queue<byte>();
        private List<SbEvent> _events = new List<SbEvent>();
        private List<SbEntity> _entities = new List<SbEntity>();

        public byte AllocateAot()
        {
            return _availableAots.Dequeue();
        }

        public byte AllocateEnemy()
        {
            return _availableEnemies.Dequeue();
        }

        public ReFlag AllocateLocalFlag()
        {

        }

        public ReFlag AllocateGlobalFlag()
        {

        }

        private void BuildInitCode(CutsceneBuilder builder)
        {
            var eventIndex = 0;
            foreach (var evt in _events)
            {
                builder.BeginProcedure($"plot_{eventIndex}");
                if (evt.Action != null)
                {
                    if (evt.StateFlag != null)
                    {
                        builder.BeginIf();
                        builder.CheckFlag(evt.StateFlag.Value, false);
                        BuildInitCode(builder, evt.Action, false);
                        builder.Else();
                        BuildInitCode(builder, evt.Action, true);
                        builder.EndIf();
                    }
                    else
                    {
                        BuildInitCode(builder, evt.Action, false);
                    }
                }
                builder.EndProcedure();
                eventIndex++;
            }
        }

        private void BuildInitCode(CutsceneBuilder builder, SbAction? action, bool state)
        {
            var found = false;
            while (action != null)
            {
                if (state)
                {
                    if (found)
                    {
                        BuildCode(builder, action);
                    }
                    else if (action is SbNextTime)
                    {
                        found = true;
                    }
                }
                else
                {
                    if (action is SbNextTime)
                    {
                        break;
                    }
                }
                action = action.Next;
            }
        }

        private void BuildCode(CutsceneBuilder builder, SbAction action)
        {
        }

        public void BuildEvent(SbEvent evt)
        {

        }

        private IEnumerable<SbAction> EnumerateActions()
        {
            foreach (var evt in _events)
            {
                var action = evt.Action;
                while (action != null)
                {
                    yield return action;
                    action = action.Next;
                }
            }
        }
    }

    public class Test
    {
        public void Build()
        {
            var ally0 = new Ally();
            var enemy0 = new Enemy();
            var enemy1 = new Enemy();
            var enemy2 = new Enemy();
            var enemy3 = new Enemy();

            // Ally event
            ally0.Action = new SbBuilder()
                .Add(new SbEnter() { DoorId = 0 })
                .Add(new SbWait() { Time = 1 })
                .Add(new SbWait() { Time = 0.5f })
                .Add(new SbTalk() { })
                .Add(new SbRun() { DestinationId = 1000 })
                .Add(new SbStoreState() { })
                .Add(new SbInteract()
                {
                    Event = new SbBuilder()
                        .Add(new SbTalk() { })
                        .Add(new SbTalk() { })
                        .Add(new SbTalk() { })
                        .Add(new SbGift() { Item = new Item(Re2ItemIds.FAidSpray, 1) })
                        .Add(new SbTalk() { })
                        .Head
                })
                .Add(new SbWait() { Time = 2 })
                .Add(new SbRun() { DestinationId = 1 })
                .Add(new SbExit() { DoorId = 1 })
                .Head;

            // Enemy event
            enemy0.Action = new SbBuilder()
                .Add(new SbEnter() { DoorId = 0 })
                .Add(new SbNextTime() { })
                .Add(new SbPosition() { })
                .Head;

            var allyEvent = new SbEvent()
            {
                TriggerCut = 2,
                Cooldown = 4,
                Action = new SbSpawnEntity() { Entity = ally0 }
            };

            var enemyEvent = new SbEvent()
            {
                TriggerCut = 1,
                Cooldown = 4,
                Action = new SbBuilder()
                    .Add(new SbSpawnEntity() { Entity = enemy0 })
                    .Add(new SbSpawnEntity() { Entity = enemy1 })
                    .Add(new SbSpawnEntity() { Entity = enemy2 })
                    .Head
            };
        }
    }
}

internal class SbEvent
{
    public ReFlag[] Flags { get; set; } = new ReFlag[0];
    public int TriggerCut { get; set; }
    public int Delay { get; set; }
    public int Cooldown { get; set; }
    public bool Reoccuring { get; set; }
    public ReFlag? StateFlag { get; set; }
    public SbAction? Action { get; set; }
}

internal class SbBuilder
{
    public SbAction? Head { get; private set; }
    public SbAction? Tail { get; private set; }

    public SbBuilder Add(SbAction action)
    {
        if (Tail == null)
        {
            Head = action;
            Tail = action;
        }
        else
        {
            Tail.Next = action;
            Tail = action;
        }
        return this;
    }
}

internal class SbEntity
{
    public byte Id { get; set; }
    public byte Type { get; set; }
    public SbAction? Action { get; set; }
}

internal class Enemy : SbEntity
{
}

internal class Ally : SbEntity
{
    public string? Actor { get; set; }
}

internal abstract class SbAction
{
    public SbAction? Next { get; set; }

    public SbAction? Tail
    {
        get
        {
            var tail = Next;
            while (tail?.Next != null)
            {
                tail = tail.Next;
            }
            return tail;
        }
    }
}

internal class SbWait : SbAction
{
    public float Time { get; set; }
}

internal class SbTalk : SbAction
{
}

internal class SbGift : SbAction
{
    public Item Item { get; set; }
}

internal class SbEnter : SbAction
{
    public int DoorId { get; set; }
}

internal class SbExit : SbAction
{
    public int DoorId { get; set; }
}

internal class SbPosition : SbAction
{
    public REPosition Position { get; set; }
}

internal class SbRun : SbAction
{
    public int DestinationId { get; set; }
    public int Timeout { get; set; }
}

internal class SbInteract : SbAction
{
    public SbAction? Event { get; set; }
}

internal class SbStoreState : SbAction
{
}

internal class SbNextTime : SbAction
{
}

internal class SbSpawnEntity : SbAction
{
    public SbEntity? Entity { get; set; }
}
}

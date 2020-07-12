using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace Jellyfish.Virtu
{
    public sealed class MachineEvent
    {
        public MachineEvent(int delta, Action action)
        {
            Delta = delta;
            Action = action;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "Delta = {0} Action = {{{1}.{2}}}", Delta, Action.Method.DeclaringType.Name, Action.Method.Name);
        }

        public int Delta { get; set; }
        public Action Action { get; set; }
    }

    public sealed class MachineEvents
    {
        static string _threadName = "";
        Machine _machine;

        public MachineEvents(Machine machine)
        {
            // needed to get DebugService
            _machine = machine;
        }

        public void AddEvent(int delta, Action action)
        {
            // _machine.DebugService.WriteMessage(action.Method.ReflectedType.FullName + "." + action.Method.Name);
            //_machine.DebugService.WriteMessage(action.Method.Name);

            // check that AddEvent() is always and only called from "Machine" thread
            if (_threadName == "") {
                _threadName = Thread.CurrentThread.Name;
                _machine.DebugService.WriteMessage( "first AddEvent() called rom '{0}'", _threadName );
            } else {
                if (Thread.CurrentThread.Name != _threadName) {
                    // caller is not the first thread which added an Event (i.e. "Machine")
                    //throw new InvalidOperationException( "Don't be evil!" );
                }
            }

            var node = _used.First;
            for (; node != null; node = node.Next)
            {
                if (delta < node.Value.Delta)
                {
                    node.Value.Delta -= delta;
                    break;
                }
                if (node.Value.Delta > 0)
                {
                    delta -= node.Value.Delta;
                }
            }

            var newNode = _free.First;
            if (newNode != null)
            {
                _free.RemoveFirst();
                newNode.Value.Delta = delta;
                newNode.Value.Action = action;
            }
            else
            {
                newNode = new LinkedListNode<MachineEvent>(new MachineEvent(delta, action));
            }

            if (node != null)
            {
                _used.AddBefore(node, newNode);
            }
            else
            {
                _used.AddLast(newNode);
            }
        }

        public int FindEvent(Action action)
        {
            int delta = 0;

            for (var node = _used.First; node != null; node = node.Next)
            {
                delta += node.Value.Delta;
                if (object.ReferenceEquals(node.Value.Action, action)) // assumes delegate cached
                {
                    return delta;
                }
            }

            return 0;
        }

        public void HandleEvents( int delta ) {
            var node = _used.First;
            node.Value.Delta -= delta;

            while (node.Value.Delta <= 0) {
                node.Value.Action();
                RemoveEvent( node );
                node = _used.First;
            }
        }

        private void RemoveEvent(LinkedListNode<MachineEvent> node)
        {
            if (node.Next != null)
            {
                node.Next.Value.Delta += node.Value.Delta;
            }

            _used.Remove(node);
            _free.AddFirst(node); // cache node; avoids garbage
        }

        private LinkedList<MachineEvent> _used = new LinkedList<MachineEvent>();
        private LinkedList<MachineEvent> _free = new LinkedList<MachineEvent>();
    }
}

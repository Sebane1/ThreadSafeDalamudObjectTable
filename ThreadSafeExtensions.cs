using Dalamud.Game.ClientState.Objects.Types;
using GameObjectHelper.ThreadSafeDalamudObjectTable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoleplayingVoiceDalamud.ThreadSafeDalamudObjectTable {
    public static class ThreadSafeExtensions {
        public static ThreadSafeGameObject ToThreadSafeObject(this IGameObject gameObject) {
            return ThreadSafeGameObjectManager.GetThreadSafeGameObject(gameObject, false);
        }
    }
}

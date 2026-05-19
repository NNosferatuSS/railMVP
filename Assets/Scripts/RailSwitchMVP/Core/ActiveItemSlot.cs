using UnityEngine;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Tipo de item ativo (= guardável, usável on-demand).
    /// Diferente de PowerUpType (que são passivos, ativam ao coletar).
    /// </summary>
    public enum ActiveItemType
    {
        None,
        TimeFreeze,
        Teleport,
    }

    /// <summary>
    /// Inventário de 1 slot pra items ativos. Pickups colocam no slot,
    /// player aperta Space (via ActiveItemInputHandler) pra usar.
    ///
    /// Stack: pickup novo SUBSTITUI o anterior (avisa via log).
    /// Use que falha (ex: Teleport sem switch direcional) NÃO consome o item.
    /// </summary>
    public class ActiveItemSlot : MonoBehaviour
    {
        public static ActiveItemSlot Instance { get; private set; }

        [SerializeField] private ActiveItemType heldItem = ActiveItemType.None;

        public ActiveItemType HeldItem => heldItem;
        public bool HasItem => heldItem != ActiveItemType.None;

        public event System.Action<ActiveItemType> OnItemAcquired;
        public event System.Action<ActiveItemType> OnItemUsed;
        public event System.Action OnItemUseFailed;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Coloca item no slot. Se já tinha um, substitui (e loga).
        /// </summary>
        public void SetItem(ActiveItemType type)
        {
            if (type == ActiveItemType.None) return;
            if (heldItem != ActiveItemType.None && heldItem != type)
                Debug.Log($"[ActiveItemSlot] Substituindo {heldItem} → {type}");
            heldItem = type;
            OnItemAcquired?.Invoke(type);
        }

        /// <summary>
        /// Usa o item do slot (não-direcional: TimeFreeze, etc.). Items
        /// direcionais como Teleport NÃO são disparados por aqui — usam
        /// UseItemWithDirection. Se o use falhar, item preservado.
        /// </summary>
        public void UseItem()
        {
            if (heldItem == ActiveItemType.None) return;
            bool consumed = ExecuteUse(heldItem);
            if (consumed)
            {
                var used = heldItem;
                heldItem = ActiveItemType.None;
                OnItemUsed?.Invoke(used);
            }
            else
            {
                if (heldItem == ActiveItemType.Teleport)
                    Debug.Log("[ActiveItemSlot] Teleport requer Shift + ← / → (direção). Space não dispara.");
                OnItemUseFailed?.Invoke();
            }
        }

        /// <summary>
        /// Usa item DIRECIONAL (Teleport). direction: -1 = left, +1 = right.
        /// Falha se item no slot não é direcional ou se a direção é inválida.
        /// </summary>
        public void UseItemWithDirection(int direction)
        {
            if (heldItem == ActiveItemType.None) return;
            bool consumed = ExecuteUseDirectional(heldItem, direction);
            if (consumed)
            {
                var used = heldItem;
                heldItem = ActiveItemType.None;
                OnItemUsed?.Invoke(used);
            }
            else
            {
                OnItemUseFailed?.Invoke();
            }
        }

        bool ExecuteUse(ActiveItemType type)
        {
            switch (type)
            {
                case ActiveItemType.TimeFreeze:
                    return TimeFreezeController.Instance != null && TimeFreezeController.Instance.TryActivate();
                // Teleport NÃO entra aqui — exige direção, vai por UseItemWithDirection.
                default:
                    return false;
            }
        }

        bool ExecuteUseDirectional(ActiveItemType type, int direction)
        {
            switch (type)
            {
                case ActiveItemType.Teleport:
                    return TeleportController.Instance != null && TeleportController.Instance.TryTrigger(direction);
                default:
                    return false;
            }
        }

        public void Clear()
        {
            heldItem = ActiveItemType.None;
        }
    }
}

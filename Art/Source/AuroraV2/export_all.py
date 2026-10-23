# Aurora V2 — exporta os meshes da coleção Aurora_V2 pros FBX que o wizard consome.
# Depois: Unity → Tools → RailSwitch → Art → "Aurora — Apply All".
import bpy

MODELS = r"C:/Users/aajun/RailMVP/Assets/Graphics/Aurora/Models/"
EXPORTS = {
    "Aurora_Track": ["Track_Ribbon"],
    "Aurora_ConnectorStraight": ["Connector_Straight"],
    "Aurora_ConnectorDiag": ["Connector_Diag"],
    "Aurora_Coin": ["Coin_Lozenge"],
    "Aurora_Gem": ["Gem_Crystal"],
    "Aurora_Player": ["Player_Pearl", "Player_Crescent"],
    "Aurora_HazardLethal": ["Hazard_Lethal"],
    "Aurora_HazardBarrier": ["Hazard_Barrier"],
    "Aurora_PowerUpRing": ["PowerUp_Ring"],
}

for fbx, names in EXPORTS.items():
    objs = [bpy.data.objects[n] for n in names]
    export_fbx(MODELS + fbx + ".fbx", objs)
    print("exported", fbx, names)

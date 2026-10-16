"""
Modelo analítico da progressão do Rail (sem rodar o jogo).

Lê os números de DifficultyConfig/RailGenConfig (copiados abaixo) e calcula, por tier:
- tempo até chegar no tier (velocidade constante, warmup ignorado);
- tempo por row e tempo de leitura (quanto do caminho à frente cabe na tela / velocidade);
- largura visível na profundidade do player vs. posição da lane mais externa
  (câmera travada em X=0) → quanto o player sai da tela.

Geometria (PlayerCameraRig): tilt T, FOV vertical F, distância Z = cameraZoom * mult,
foco = player + up * Z * lookAhead. Profundidade do player ao longo do forward = Z, então
meia-largura visível no player = Z * tan(hFOV/2), hFOV/2 = atan(tan(F/2) * aspect).
Alcance à frente: interseção do raio do topo da tela com o plano y=0.
"""
import math
import sys

LANE = 3.0
ROW = 12 + 2          # trackLength + rowGap
TILE_HALF_W = 0.68
MULT = 2.3
TILT, FOV, LOOK = 50.0, 50.0, 0.3

CURRENT = [  # trigger, maxLanes, speed, zoom
    (0, 3, 5, 10), (30, 3, 6, 11), (100, 4, 7, 12), (210, 5, 9, 13), (400, 5, 11, 13),
    (650, 6, 13, 15.5), (1000, 7, 16, 18), (1600, 8, 22, 18), (2500, 9, 22, 18),
    (3200, 9, 26, 20), (3200, 10, 30, 22),
]

PROPOSED = [
    (0, 3, 5, 10), (30, 3, 6, 11), (100, 4, 7.5, 12), (210, 5, 9, 13), (400, 5, 10.5, 13.5),
    (650, 6, 12, 15), (1000, 7, 14, 16.5), (1500, 7, 16, 17.5), (2200, 8, 18, 18.5),
    (3000, 9, 20.5, 19.5), (4000, 9, 23, 20.5), (5200, 9, 25, 21.5),
]


def geometry(zoom, aspect):
    z = zoom * MULT
    t = math.radians(TILT)
    fwd = (0.0, -math.sin(t), math.cos(t))
    up = (0.0, math.cos(t), math.sin(t))
    cam_y = up[1] * z * LOOK - fwd[1] * z
    cam_z = up[2] * z * LOOK - fwd[2] * z
    top = math.radians(TILT - FOV / 2)              # raio do topo, abaixo do horizonte
    ahead = cam_z + cam_y / math.tan(top)           # z do chão no topo da tela (rel. player)
    half_w = z * math.tan(math.atan(math.tan(math.radians(FOV / 2)) * aspect))
    return z, ahead, half_w


def lane_extent(max_lanes, global_max=9):
    n = min(max_lanes, global_max)
    lo = (global_max - n) // 2
    hi = lo + n - 1
    xs = [(l - (global_max - 1) / 2) * LANE for l in (lo, hi)]
    return max(abs(x) for x in xs)


def report(name, tiers, follow=0.0):
    print(f"\n=== {name} {'(follow lateral %.2f)' % follow if follow else '(câmera travada X=0)'} ===")
    print("tier  dist  lanes vel  zoom  t_tier  t_cum  s/row  leitura  vis_rows  saida_9:19.5  saida_9:16")
    cum = 0.0
    for i, (trig, lanes, v, zoom) in enumerate(tiers):
        nxt = tiers[i + 1][0] if i + 1 < len(tiers) else None
        dur = (nxt - trig) / v if nxt is not None and nxt > trig else float("nan")
        z, ahead, _ = geometry(zoom, 0.4615)
        ext = lane_extent(lanes) + TILE_HALF_W
        outs = []
        for aspect in (0.4615, 0.5625):
            _, _, hw = geometry(zoom, aspect)
            cam_x = ext * follow
            outs.append(max(0.0, (ext - cam_x) - hw))
        print(f"{i:>4} {trig:>5} {lanes:>5} {v:>4} {zoom:>5} {dur:>7.1f} {cum:>6.1f} {ROW / v:>6.2f} "
              f"{ahead / v:>7.2f}s {ahead / ROW:>8.1f} {outs[0]:>12.2f} {outs[1]:>11.2f}")
        if not math.isnan(dur):
            cum += dur


if __name__ == "__main__":
    report("ATUAL", CURRENT)
    report("PROPOSTO", PROPOSED)
    report("PROPOSTO", PROPOSED, follow=float(sys.argv[1]) if len(sys.argv) > 1 else 0.6)

"""Generate the board specification diagram from scene coordinates (stdlib only).

Run: python Tools/GenerateYutBoardDiagram.py
The SVG embeds the existing background so it can be opened independently.
This documents the intended graph; it does not change runtime movement.
"""
from pathlib import Path
import base64
import math
import re

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / "Assets/Scenes/ThrowYut.unity"
BOARD = ROOT / "Assets/Scripts/Yut/YutBoard.cs"
OUTPUT = ROOT / "Docs/Diagrams/YutBoardNodes.svg"


def load_nodes():
    blocks = dict(re.findall(r"^--- !u!\d+ &(\d+)\n(.*?)(?=^--- !u!|\Z)",
                             SCENE.read_text(encoding="utf-8"), re.M | re.S))
    nodes = {}
    for block in blocks.values():
        match = re.search(r"  m_Name: Cell (\d\d)\n", block)
        if not match:
            continue
        components = re.findall(r"component: \{fileID: (\d+)\}", block)
        cell = next(blocks[c] for c in components if "cellId:" in blocks[c])
        cell_id = int(re.search(r"cellId: (\d+)", cell)[1])
        assert cell_id == int(match[1]) and cell_id not in nodes
        transform = blocks[components[0]]
        xyz = re.search(r"m_LocalPosition: \{x: (.*?), y: (.*?), z: (.*?)\}", transform)
        position = tuple(map(float, xyz.groups()))
        assert position[2] == 0
        # Local coordinates are world coordinates only while the ancestors are identity.
        ancestor = transform
        while True:
            parent = re.search(r"m_Father: \{fileID: (\d+)\}", ancestor)[1]
            if parent == "0":
                break
            ancestor = blocks[parent]
            for field, expected in (("m_LocalPosition", "{x: 0, y: 0, z: 0}"),
                                    ("m_LocalScale", "{x: 1, y: 1, z: 1}"),
                                    ("m_LocalRotation", "{x: 0, y: 0, z: 0, w: 1}")):
                assert f"{field}: {expected}" in ancestor, (cell_id, parent, field)
        nodes[cell_id] = position
    assert set(nodes) == set(range(29))
    return nodes


def load_edges():
    source = BOARD.read_text(encoding="utf-8")
    forward_source = source.split("private static readonly int[][] NextCells =", 1)[1].split("};", 1)[0]
    entries = re.findall(r"new\[\] \{ ([\w, -]+) \}, // (\d+)", forward_source)
    forward = {int(i): [(-1 if v.strip() == 'ExitCellId' else int(v)) for v in values.split(',')]
               for values, i in entries}
    assert set(forward) == set(range(29)) and forward[0] == [-1]
    assert forward[5] == [6, 20] and forward[10] == [11, 25]
    assert forward[22] == [23, 27]
    # The virtual exit is drawn separately, never as one of the 29 occupied nodes.
    forward[0] = []
    backward = {i: [] for i in forward}
    for start, destinations in forward.items():
        for end in destinations:
            assert end in backward
            backward[end].append(start)
    # Explicit start -> last backward boundary from chapter04. No exit/reward.
    backward[1] = [0]
    assert backward[0] == [19, 28] and backward[22] == [21, 26]
    backward_source = source.split("private static readonly int[][] BackwardCells =", 1)[1].split("};", 1)[0]
    actual_backward = {int(i): list(map(int, values.split(","))) for values, i in
                       re.findall(r"new\[\] \{ ([\d, ]+) \}, // (\d+)", backward_source)}
    assert actual_backward == backward, "Runtime backward graph differs from the documented graph"
    return forward, backward


def generate():
    nodes = load_nodes()
    forward, backward = load_edges()
    background = base64.b64encode((ROOT / "Assets/Art/Background/Background.png").read_bytes()).decode()
    parts = ['<svg xmlns="http://www.w3.org/2000/svg" width="1880" height="930" viewBox="0 0 1880 930">',
             '<rect width="1880" height="930" fill="#101c2b"/>',
             '<style>text{font-family:Malgun Gothic,sans-serif;fill:white} .node{font-size:19px;font-weight:bold;text-anchor:middle;dominant-baseline:central}</style>',
             '<text x="40" y="44" font-size="27">윷판 노드 대응 · 작업01 / 기획 규칙 도식 (런타임 적용은 후속 작업)</text>']

    def label(x, y, value, size=19):
        parts.append(f'<text x="{x}" y="{y}" font-size="{size}">{value}</text>')

    for panel, (edges, title, color) in enumerate(((forward, "전진 · 화살표는 한 칸 이동", "#65e8ff"),
                                                 (backward, "빽도 · 화살표는 허용 후퇴 후보", "#ffbf69"))):
        offset = 35 + panel * 940
        label(offset, 88, title, 24)
        parts.append(f'<svg x="{offset}" y="110" width="870" height="660" viewBox="170 290 940 700">')
        parts.append(f'<image width="1920" height="1080" href="data:image/png;base64,{background}"/>')
        points = {i: (960 + x * 108, 540 - y * 108) for i, (x, y, _) in nodes.items()}
        for start, destinations in edges.items():
            for end in destinations:
                x, y = points[start]
                ex, ey = points[end]
                dx, dy = ex - x, ey - y
                length = math.hypot(dx, dy)
                ux, uy = dx / length, dy / length
                sx, sy = x + ux * 23, y + uy * 23
                tx, ty = ex - ux * 25, ey - uy * 25
                parts.append(f'<path d="M {sx},{sy} L {tx},{ty}" stroke="#102030" stroke-width="9"/>')
                parts.append(f'<path d="M {sx},{sy} L {tx},{ty}" stroke="{color}" stroke-width="5"/>')
                parts.append(f'<path d="M {tx},{ty} l {-ux*13-uy*7},{-uy*13+ux*7} l {uy*14},{-ux*14} Z" fill="{color}"/>')
        for i, (x, y) in points.items():
            fill = "#11734d" if i == 1 else "#a53249" if i == 0 else "#293f72" if i in (5, 10, 22) else "#172332"
            parts.append(f'<circle cx="{x}" cy="{y}" r="23" fill="{fill}" stroke="white" stroke-width="2"/>')
            parts.append(f'<text class="node" x="{x}" y="{y}">{i:02}</text>')
        if panel == 0:
            x, y = points[0]
            parts.append(f'<path d="M {x},{y+24} v 58" stroke="#ff738e" stroke-width="6"/>')
            parts.append(f'<path d="M {x},{y+91} l -10,-15 h 20 Z" fill="#ff738e"/>')
            label(x - 120, y + 121, "퇴장 · 전진 초과 시에만", 21)
        parts.append('</svg>')
    label(40, 800, "01 초록 = 소환 시작 칸    00 빨강 = 마지막 칸    05 / 10 / 22 파랑 = 전진 시작 시 분기", 22)
    label(40, 839, "전진: 00에 정확히 멈추면 유지 / 다음 한 칸부터 퇴장. 이동 도중 05·10은 바깥길, 22는 진입 대각선 유지.")
    label(40, 875, "후퇴: 이동 이력 무관. 01 → 00은 무보상 / 00 → 19 또는 28 / 15 → 14 또는 24 / 22 → 21 또는 26.")
    label(40, 910, "실제 씬 Cell 00~28 좌표 + 기존 전진 간선 대조. 전체 좌표·연결표와 현재 코드 차이는 Docs/YutBoardSpec.md 참조.", 17)
    parts.append('</svg>')
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text('\n'.join(parts), encoding="utf-8")
    print(f"Verified 29 scene cells; wrote {OUTPUT}")
    print("| Cell | 월드 X | 월드 Y | 배경 픽셀 X,Y | 전진 1칸 | 허용 빽도 |")
    print("| --- | ---: | ---: | --- | --- | --- |")
    for i in sorted(nodes):
        x, y, _ = nodes[i]
        fwd = ', '.join(f'{v:02}' for v in forward[i]) or '퇴장'
        back = ', '.join(f'{v:02}' for v in backward[i])
        print(f"| {i:02} | {x:.6f} | {y:.6f} | {round(960+x*108)}, {round(540-y*108)} | {fwd} | {back} |")


if __name__ == "__main__":
    generate()

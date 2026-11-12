"""Phase 19: replaces literal colours in Realmkeeper's two HUD stylesheets with design tokens.

Run with any Python 3:  python -I Tools/UI/tokenize_uss.py <file.uss> [...]
Colours not in the table stay literal (they are rare, one-off accents) and are reported.
"""
import re
import sys

# (r, g, b) or (r, g, b, a) -> token. Families of near-identical values collapse onto one token.
TABLE = {
    # Primary text
    (232, 223, 204): "--rk-text", (236, 222, 186): "--rk-text", (232, 224, 206): "--rk-text",
    (240, 230, 205): "--rk-text", (236, 226, 200): "--rk-text", (226, 214, 190): "--rk-text",
    (214, 205, 186): "--rk-text", (250, 236, 206): "--rk-text",
    # Secondary text
    (190, 181, 163): "--rk-text-2", (170, 161, 144): "--rk-text-2", (170, 160, 140): "--rk-text-2",
    (170, 160, 136): "--rk-text-2", (160, 150, 128): "--rk-text-2",
    # Muted text
    (150, 142, 126): "--rk-text-3", (120, 114, 102): "--rk-text-3",
    # Headings and accents
    (240, 214, 160): "--rk-heading", (240, 220, 170): "--rk-heading", (226, 196, 120): "--rk-heading",
    (214, 188, 120): "--rk-heading", (214, 180, 116): "--rk-bronze-hi", (240, 200, 110): "--rk-bronze-hi",
    (230, 190, 100): "--rk-bronze-hi", (201, 164, 101): "--rk-bronze", (240, 214, 160, 0.8): "--rk-heading",
    (214, 180, 116, 0.6): "--rk-bronze",
    # Lines
    (201, 164, 101, 0.45): "--rk-line-strong", (201, 164, 101, 0.4): "--rk-line-strong",
    (201, 164, 101, 0.35): "--rk-line", (201, 164, 101, 0.3): "--rk-line", (201, 164, 101, 0.28): "--rk-line",
    (201, 164, 101, 0.2): "--rk-line-soft", (201, 164, 101, 0.15): "--rk-line-soft", (201, 164, 101, 0.12): "--rk-line-soft",
    # Surfaces
    (22, 20, 17, 0.84): "--rk-panel", (18, 16, 13, 0.9): "--rk-window", (18, 16, 13, 0.85): "--rk-panel",
    (18, 16, 12, 0.72): "--rk-panel", (40, 36, 30, 0.9): "--rk-card", (34, 31, 26, 0.7): "--rk-card",
    (44, 40, 33, 0.9): "--rk-card",
    # Controls
    (60, 54, 45, 0.9): "--rk-control", (84, 75, 60, 0.95): "--rk-control-hover", (84, 75, 60, 0.7): "--rk-control-hover",
    (110, 96, 72, 1): "--rk-control-active", (120, 92, 48, 0.95): "--rk-bronze-deep", (142, 110, 58, 1): "--rk-bronze-deep-hi",
    (120, 92, 48, 0.55): "--rk-bronze-deep",
    # States
    (236, 120, 96): "--rk-bad", (232, 128, 104): "--rk-bad", (150, 52, 40, 0.9): "--rk-bad-bg",
    (160, 200, 120): "--rk-good", (126, 196, 112): "--rk-good", (170, 210, 150): "--rk-good",
    (232, 168, 72): "--rk-warn", (150, 182, 236): "--rk-info", (96, 128, 140, 0.85): "--rk-info-bg",
}

PATTERN = re.compile(r"rgba?\(\s*([0-9.]+)\s*,\s*([0-9.]+)\s*,\s*([0-9.]+)\s*(?:,\s*([0-9.]+)\s*)?\)")


def key(m):
    r, g, b, a = m.group(1), m.group(2), m.group(3), m.group(4)
    base = (int(float(r)), int(float(g)), int(float(b)))
    if a is None or float(a) == 1 and base + (1,) not in TABLE:
        return base
    return base + (float(a) if float(a) != 1 else 1,)


def main(paths):
    for path in paths:
        text = open(path, encoding="utf-8").read()
        left = {}

        def repl(m):
            k = key(m)
            token = TABLE.get(k)
            if token is None:
                left[m.group(0)] = left.get(m.group(0), 0) + 1
                return m.group(0)
            return "var(%s)" % token

        new = PATTERN.sub(repl, text)
        open(path, "w", encoding="utf-8", newline="\n").write(new)
        print(path, "literal colours left:", sum(left.values()), "distinct:", len(left))


if __name__ == "__main__":
    main(sys.argv[1:])

"""Pixel-authored apprentice artwork on the project's 64×64 canvas.

The integer shapes below are the artwork source. They draw directly to an RGBA
pixel grid. The selection illustration adds smaller material accents at 3×.
"""
from PIL import Image, ImageDraw

FAMILIES = ("dalgu", "chorong", "galpi", "seonge", "geumeum")
INK = ("#2A1A20", "#1C1830", "#1A2030", "#1E2238", "#1B0D1B")
SKIN = ("#F2CFA0", "#F0CFA8", "#F2D2AC", "#F4D6B4", "#7A336A")
SKIN_DARK = ("#C69574", "#C4967B", "#C8987D", "#C8A292", "#561F4C")
ACCENT = ("#E39A4E", "#FFF36B", "#4FE3C1", "#9FDCFF", "#E2ACFA")
GROUND = (30, 60)
SOCKETS = {
    "dalgu": ((44, 44), (43, 43), (42, 45)),
    "chorong": ((43, 44), (40, 44), (42, 45)),
    "galpi": ((44, 46), (43, 45), (42, 46)),
    "seonge": ((45, 44), (44, 43), (42, 45)),
    "geumeum": ((44, 45), (43, 45), (43, 45)),
}
ANCHORS = {
    "dalgu": ((29, 14), (28, 13), (29, 14)),
    "chorong": ((28, 15), (30, 15), (31, 15)),
    "galpi": ((30, 16), (29, 16), (30, 16)),
    "seonge": ((30, 13), (30, 13), (30, 13)),
    "geumeum": ((30, 13), (30, 13), (30, 13)),
}
DIRECTIONS = ("down", "right", "up")


class Painter:
    def __init__(self, scale, ink):
        self.scale = scale
        self.ink = ink
        self.image = Image.new("RGBA", (64 * scale, 64 * scale))
        self.d = ImageDraw.Draw(self.image)

    def points(self, points):
        return [(round(x * self.scale), round(y * self.scale)) for x, y in points]

    def fill(self, color, points):
        self.d.polygon(self.points(points), fill=color)

    def shape(self, color, points):
        self.d.polygon(self.points(points), fill=color, outline=self.ink,
                       width=1 if self.scale == 1 else 2)

    def line(self, color, points, width=1):
        self.d.line(self.points(points), fill=color, width=max(1, round(width * self.scale)))

    def rect(self, color, x, y, w, h):
        a, b = self.points(((x, y), (x + w, y + h)))
        self.d.rectangle((a, (b[0] - 1, b[1] - 1)), fill=color)

    def stitches(self, color, x, y, count):
        for i in range(count):
            self.rect(color, x + i * 2, y + i % 2, 1, 1)


def boots(p, direction, color, light, dark):
    for x, y, w in [(20, 54, 10), (32 if direction != "right" else 33, 54, 10)]:
        if direction == "right" and x == 20:
            y, w = 53, 11
        p.shape(color, [(x+1,y),(x+w-3,y),(x+w-1,y+2),(x+w,y+4),
                        (x+w-1,y+5),(x,y+5),(x-1,y+3),(x-1,y+1)])
        p.fill(dark, [(x,y+4),(x+w-1,y+4),(x+w-1,y+5),(x,y+5)])
        p.rect(light, x+2, y+2, w-5, 1)


def mitt(p, family, x, y, side=False):
    i = FAMILIES.index(family)
    p.shape(SKIN_DARK[i], [(x-3,y-3),(x+1,y-3),(x+3,y-1),(x+3,y+2),(x+1,y+3),(x-2,y+2),(x-3,y)])
    p.fill(SKIN[i], [(x-2,y-2),(x+1,y-2),(x+2,y-1),(x+2,y+1),(x,y+2),(x-2,y+1)])
    if side:
        p.rect(SKIN_DARK[i], x+1, y, 2, 1)


def face(p, family, direction, y, mood):
    i = FAMILIES.index(family)
    if direction == "up":
        return
    if direction == "right":
        p.shape(SKIN_DARK[i], [(20,y-1),(36,y-1),(42,y+2),(43,y+8),
                               (46,y+10),(44,y+13),(41,y+15),(30,y+16),(23,y+12)])
        p.fill(SKIN[i], [(27,y),(35,y),(40,y+3),(41,y+8),(44,y+10),(42,y+12),
                        (37,y+13),(28,y+13),(25,y+9)])
        eyes = [39]
    else:
        p.shape(SKIN_DARK[i], [(17,y),(38,y-1),(43,y+4),(43,y+10),
                              (38,y+15),(24,y+15),(18,y+11),(16,y+6)])
        p.fill(SKIN[i], [(18,y+2),(39,y+1),(41,y+5),(41,y+10),(36,y+13),
                        (24,y+13),(19,y+10)])
        eyes = [23, 35]
    for x in eyes:
        if family in ("chorong", "seonge") or mood == "blink":
            p.line(p.ink, [(x, y+8),(x+3,y+8 if family == "chorong" else y+7)])
        elif mood == "happy":
            p.line(p.ink, [(x,y+8),(x+1,y+6),(x+3,y+7)])
        else:
            p.rect(p.ink, x, y+6, 2, 3)
            p.rect("#FFF0C9", x, y+6, 1, 1)
            if mood == "strained":
                p.line(p.ink, [(x-1,y+4),(x+2,y+5)])
    mouth = 41 if direction == "right" else 29
    p.line("#825456", [(mouth-1,y+11),(mouth+1,y+11+(mood == "happy"))])
    if family == "dalgu" and direction == "down":
        p.rect("#745451", 38, y+10, 1, 1)


def kettle(p, direction, mood, parts):
    body = "#1F8C82"
    boots(p, direction, "#E8E4DA", "#FFF4DC", "#B6AAA0")
    p.shape(body, [(22,34),(37,34),(44,39),(43,45),(48,52),(44,55),
                   (36,56),(23,56),(16,53),(18,46),(16,42)])
    p.fill("#16655F", [(37,38),(43,39),(41,46),(45,52),(41,54),(37,55),(39,49)])
    p.line("#4FC2B0", [(18,52),(23,54),(37,54),(44,52)])
    if direction == "up":
        p.line("#B5692F", [(21,38),(37,49),(42,45)], 3)
        p.line("#E39A4E", [(38,37),(24,47),(22,51)], 2)
        p.line("#5A3A2A", [(28,48),(36,48),(34,52)], 3)
    elif direction == "right":
        p.shape("#5A3A2A", [(33,38),(39,39),(41,45),(45,51),(35,52),(31,47)])
        p.fill("#B9936D", [(35,40),(38,41),(40,45),(42,50),(35,50)])
        p.shape("#E8E4DA", [(26,37),(33,38),(36,42),(34,45),(27,45),(24,43),(24,39)])
        p.line("#FFF4DC", [(26,39),(28,38),(32,39)])
    else:
        p.shape("#5A3A2A", [(24,38),(37,38),(39,43),(42,52),(37,54),(24,54),(20,51)])
        p.fill("#B9936D", [(25,40),(35,40),(37,44),(39,51),(34,52),(24,52),(23,50)])
        p.shape("#A85535", [(27,46),(35,46),(35,50),(33,51),(28,50)])
        p.line("#CA8652", [(27,47),(34,47)])
        p.line("#E8E4DA", [(21,39),(19,41),(19,44)], 3)
        p.rect("#FFD79A", 24, 40, 2, 2)
        p.rect("#FFD79A", 36, 40, 2, 2)
    face(p, "dalgu", direction, 23, mood)
    hair = [(14,24),(23,25),(23,33),(20,36),(17,38),(12,36),(11,31)]
    if direction == "up":
        hair = [(14,23),(43,23),(46,28),(44,35),(39,38),(32,36),(26,38),(22,37),(17,38),(12,34)]
    p.shape("#16655F", hair)
    p.line("#1F8C82", [(15,27),(15,34),(19,35)] if direction != "up" else [(16,29),(22,34),(30,33),(37,35),(42,32)], 3)
    if direction == "down":
        p.shape("#16655F", [(38,25),(45,25),(48,31),(48,35),(43,38),(39,36),(40,32)])
        p.line("#4FC2B0", [(44,30),(45,33),(44,35)])
    flip = direction == "up"
    def orient(points):
        return [(61-x if flip else x,y) for x,y in points]
    p.shape("#B5692F", orient([(13,22),(15,16),(20,12),(27,10),(36,10),(43,13),(47,18),
            (49,16),(51,11),(56,10),(58,12),(56,16),(51,22),(47,23),(43,27),(34,29),(17,28),(12,26)]))
    p.fill("#7A4322", orient([(45,17),(49,18),(52,15),(54,12),(56,12),(54,16),(50,21),
                            (46,22),(44,26),(36,28),(19,27),(16,25),(35,25),(41,23)]))
    p.fill("#E39A4E", orient([(18,18),(21,15),(28,13),(34,13),(40,15),(42,18),
                             (41,21),(36,23),(23,24),(16,22)]))
    p.fill("#FFD79A", orient([(21,17),(23,16),(27,16),(26,18),(23,19),(21,18)]))
    p.line("#E39A4E", orient([(15,25),(21,27),(35,27),(42,25)]))
    p.line("#FFD79A", orient([(49,18),(53,14),(55,13)]))
    if "kettle_lid" not in parts:
        p.shape("#B5692F", orient([(20,12),(24,9),(28,8),(35,8),(39,9),(42,12),(37,14),(25,14)]))
        p.line("#E39A4E", orient([(23,12),(28,10),(35,10),(38,11)]))
        p.shape("#E39A4E", orient([(28,9),(27,7),(29,5),(32,5),(34,7),(33,9)]))
        p.line("#FFD79A", orient([(29,6),(31,6)]))
    mitt(p, "dalgu", *SOCKETS["dalgu"][DIRECTIONS.index(direction)], direction == "right")


def lantern(p, direction, mood, parts):
    boots(p, direction, "#4A3A2A", "#A27D66", "#38264C")
    p.shape("#C89A22", [(23,33),(36,33),(41,37),(44,43),(42,46),(45,53),
                        (40,56),(21,56),(17,53),(20,46),(18,44),(20,38)])
    p.fill("#8A6A14", [(35,36),(41,39),(40,46),(43,52),(40,55),(35,55),(36,51),(35,46)])
    p.fill("#EBC04A", [(24,38),(28,37),(30,41),(25,50),(20,51),(22,46),(21,42)])
    p.line("#6FE0C0", [(19,52),(24,54),(37,54),(43,52)], 2)
    if direction != "up":
        p.line("#8A6A14", [(24,37),(29,40),(34,37)], 2)
        p.line("#EBC04A", [(20,42),(23,40),(24,42)])
    else:
        p.line("#8A6A14", [(32,37),(34,44),(34,51)], 2)
        p.line("#EBC04A", [(24,39),(21,48)])
    face(p, "chorong", direction, 23, mood)
    p.shape("#38264C", [(17,23),(24,24),(27,29),(23,34),(20,36),(15,34),(13,29)])
    if direction == "up":
        p.shape("#38264C", [(16,24),(41,25),(42,31),(38,35),(26,37),(20,35),(15,33)])
        p.line("#4B3548", [(18,28),(24,32),(31,33),(38,30)], 2)
    reverse = direction == "right"
    def orient(points):
        return [(61-x if reverse else x,y) for x,y in points]
    p.shape("#5A45A0", orient([(12,26),(15,17),(20,12),(26,9),(35,8),(42,10),(48,13),
            (51,18),(53,22),(51,26),(47,25),(46,21),(41,20),(41,24),(43,26),(39,28),(17,29)]))
    p.fill("#3A2A6A", orient([(41,12),(48,15),(51,20),(51,24),(48,24),(45,19),
                            (39,17),(35,19),(34,22),(23,25),(17,25),(22,21),(28,19),(30,15)]))
    p.fill("#8068C8", orient([(18,18),(23,13),(28,11),(35,10),(42,12),(44,14),
                             (38,13),(31,13),(24,15),(20,18)]))
    p.line("#6FE0C0", orient([(14,25),(22,24),(34,23),(40,24)]), 2)
    p.line("#4B3548", orient([(48,25),(49,28),(50,29)]))
    if "lantern" not in parts:
        p.shape("#C89A22", orient([(47,30),(49,28),(52,28),(54,31),(53,35),(50,36),(47,34)]))
        p.fill("#FFF36B", orient([(48,31),(50,30),(52,31),(52,34),(50,35),(48,34)]))
        p.line("#FFFFFF", orient([(49,31),(49,33)]))
    mitt(p, "chorong", *SOCKETS["chorong"][DIRECTIONS.index(direction)], direction == "right")


def book(p, direction, mood, parts):
    boots(p, direction, "#453345", "#BCACA1", "#2B314A")
    p.shape("#55657C", [(22,36),(38,35),(43,39),(44,44),(41,48),(45,53),
                        (40,56),(19,56),(16,54),(20,47),(17,44),(18,39)])
    p.fill("#3A4658", [(33,38),(40,39),(43,43),(39,48),(43,53),(38,55),(32,54),
                       (34,50),(29,47),(30,42)])
    p.line("#7C8EA6", [(19,53),(23,54),(35,54),(40,53)])
    if direction == "up":
        p.line("#3A4658", [(21,38),(28,42),(35,42),(40,38)], 3)
        p.line("#7C8EA6", [(25,40),(30,41),(36,40)])
    else:
        p.line("#7C8EA6", [(19,41),(22,39),(26,40),(27,43)], 2)
    p.line("#5A3A2E", [(20,47),(26,48),(33,47),(42,46)], 3)
    p.shape("#C8A24A", [(34,45),(42,44),(45,45),(45,48),(43,50),(35,50),(33,48)])
    p.line("#EDE3C8", [(36,46),(42,45)])
    p.rect("#8A5A3E", 35,47,2,2)
    face(p, "galpi", direction, 25, mood)
    if direction == "up":
        p.shape("#453345", [(15,24),(43,24),(46,29),(43,36),(35,40),(24,38),(18,35),(16,31)])
        p.line("#5A3A2E", [(19,31),(26,35),(35,35),(42,31)], 3)
    else:
        p.shape("#5A3A2E", [(13,22),(23,24),(26,27),(20,28),(20,33),(16,36),(14,32)])
        if direction == "down":
            p.line("#5A3A2E", [(37,25),(42,27),(43,32),(41,34)], 3)
    if direction == "right":
        cover = [(6,22),(15,17),(22,11),(29,10),(35,12),(46,20),(49,27),
                 (46,29),(36,21),(28,25),(20,32),(8,31),(5,27)]
        pages = [(7,27),(19,28),(27,23),(35,18),(45,25),(48,28),(46,30),
                 (35,22),(28,27),(20,33),(8,32)]
        fold = [(8,23),(18,19),(25,13),(29,13),(35,16),(28,18),(19,25),(9,28)]
    else:
        cover = [(5,25),(8,20),(16,15),(23,11),(28,11),(30,13),(33,11),
                 (38,11),(45,15),(51,20),(55,25),(53,30),(49,31),(38,25),
                 (30,19),(22,25),(12,31),(7,30)]
        pages = [(7,26),(11,26),(20,21),(27,16),(31,17),(39,22),(51,28),
                 (53,29),(50,32),(40,28),(30,21),(22,28),(12,32),(7,31)]
        fold = [(9,23),(17,18),(25,14),(28,14),(25,17),(18,22),(9,26)]
    p.shape("#8A5A3E", cover)
    p.fill("#B88A5A", fold)
    p.fill("#5A3A2E", [(31,14),(38,14),(47,20),(51,25),(48,25),(39,20),(32,18)])
    p.shape("#EDE3C8", pages)
    p.line("#C2AA91", [(8,29),(16,29),(22,26),(30,20)] if direction != "right" else [(9,29),(19,30),(29,23),(34,20)])
    p.line("#5A3A2E", [(30,12),(30,15),(31,17)], 2)
    p.rect("#C8A24A", 28, 13, 4, 2)
    p.rect("#EDE3C8", 29, 13, 2, 1)
    if "book_ribbon" not in parts:
        x = 43 if direction != "right" else 14
        p.shape("#4FE3C1", [(x,22),(x+3,23),(x+3,35),(x+1,33),(x-1,34),(x-1,27)])
        p.line("#28887D", [(x+2,24),(x+2,32)])
    mitt(p, "galpi", *SOCKETS["galpi"][DIRECTIONS.index(direction)], direction == "right")


def crystal(p, x, y, size=4):
    p.shape("#9FDCFF", [(x,y-size),(x+2,y-2),(x+3,y+1),(x,y+size),
                       (x-3,y+1),(x-2,y-2)])
    p.fill("#F4FBFF", [(x,y-size+1),(x,y+1),(x-2,y+1),(x-1,y-2)])
    p.line("#4F7CE0", [(x+2,y+1),(x+1,y+3)])


def frost(p, direction, mood, parts):
    boots(p, direction, "#7A5A3A", "#BC9168", "#51404A")
    p.shape("#2E55B8", [(22,31),(36,31),(40,38),(43,52),(39,55),(21,55),
                        (16,52),(18,44),(19,38)])
    p.fill("#4F7CE0", [(23,37),(28,36),(30,47),(29,54),(22,53),(19,51)])
    if direction == "up":
        p.shape("#F3E7C0", [(19,31),(38,30),(43,37),(44,45),(47,53),
                            (40,57),(24,57),(13,55),(10,52),(15,43)])
        p.fill("#C9B88A", [(37,33),(42,38),(40,47),(34,53),(22,56),(15,55),(29,49)])
        p.line("#9FDCFF", [(12,53),(23,56),(37,56),(45,53)], 2)
        p.line("#FFF8E2", [(18,39),(15,47),(14,51),(21,52)])
    elif direction == "right":
        p.shape("#F3E7C0", [(21,31),(34,31),(40,35),(38,39),(33,40),(31,49),
                            (26,55),(18,57),(10,56),(12,50),(16,41)])
        p.fill("#9FDCFF", [(33,38),(36,40),(33,50),(27,56),(18,57),(23,54),(27,49)])
        p.fill("#C9B88A", [(18,37),(22,34),(29,33),(25,40),(20,47),(14,52),(14,49)])
        p.line("#FFF8E2", [(19,43),(24,38),(29,36)], 2)
    else:
        p.shape("#F3E7C0", [(21,31),(28,32),(30,36),(24,42),(21,51),(16,56),(9,55),(13,48),(15,40)])
        p.shape("#F3E7C0", [(32,35),(37,31),(43,34),(44,41),(46,47),(50,54),
                            (45,56),(39,54),(35,44)])
        p.line("#9FDCFF", [(28,37),(23,43),(20,51),(15,55)], 2)
        p.line("#9FDCFF", [(34,39),(37,46),(40,53),(45,55)], 2)
        p.line("#C9B88A", [(39,35),(42,40),(45,48)], 2)
        p.line("#FFF8E2", [(19,36),(17,41),(16,46)], 2)
        p.rect("#F4FBFF", 29, 36, 4, 2)
    face(p, "seonge", direction, 19, mood)
    hair = [(16,23),(18,17),(23,13),(29,14),(35,11),(38,11),(37,15),(42,14),
            (44,17),(43,21),(45,24),(41,28),(37,27),(32,23),(26,26),(22,31),(17,30)]
    if direction == "up":
        hair = [(15,22),(18,16),(23,13),(28,14),(35,11),(39,11),(38,15),(44,16),
                (44,21),(47,24),(44,31),(41,34),(34,37),(29,36),(24,36),(19,33),(16,30)]
    p.shape("#E4DAFF", hair)
    p.fill("#B9A8E8", [(17,24),(21,26),(25,24),(29,21),(35,19),(32,24),(27,27),(22,31),(18,30)])
    if direction == "up":
        p.fill("#B9A8E8", [(17,27),(22,29),(30,31),(35,28),(41,28),(44,25),
                           (43,32),(38,35),(33,36),(23,34),(19,31)])
        p.line("#A18A9B", [(21,31),(25,33),(30,34),(34,33)], 2)
    p.line("#F4FBFF", [(21,20),(25,17),(30,17),(35,14)])
    if "crown" not in parts:
        for x, y, r in [(14,16,3),(21,9,4),(30,5,4),(39,8,4),(47,15,3)]:
            crystal(p, x, y, r)
    mitt(p, "seonge", *SOCKETS["seonge"][DIRECTIONS.index(direction)], direction == "right")


def shadow(p, direction, mood, parts):
    boots(p, direction, "#704456", "#A47384", "#44223D")
    p.shape("#7A336A", [(22,33),(37,33),(43,39),(45,46),(46,52),(42,56),
                        (22,56),(14,54),(16,48),(19,42)])
    p.fill("#561F4C", [(34,37),(41,37),(43,45),(43,49),(45,53),
                       (40,55),(36,55),(38,52),(34,47)])
    p.line("#B25A9B", [(29,46),(25,52),(21,54)])
    if direction == "up":
        cape = [(20,31),(36,31),(43,35),(44,43),(47,51),(43,54),(36,55),(22,54),(13,50),(16,41)]
        p.shape("#3E4675", cape)
        p.fill("#222641", [(15,47),(23,49),(31,50),(40,47),(44,44),(46,51),
                            (42,53),(33,54),(23,52),(17,51)])
        p.line("#6271AE", [(16,44),(23,46),(31,46),(38,44),(42,42)], 2)
    else:
        cape = [(20,31),(30,33),(40,33),(44,36),(47,43),(42,47),(38,42),
                (31,43),(23,51),(17,55),(10,53),(9,50),(13,45),(16,38)]
        p.shape("#3E4675", cape)
        p.fill("#222641", [(13,47),(22,44),(29,40),(37,38),(40,41),(32,44),
                            (23,51),(15,54),(11,52)])
        p.line("#6271AE", [(12,50),(19,49),(26,44),(32,41),(38,39)], 2)
        p.line("#6271AE", [(39,40),(42,42),(44,45)])
        p.shape("#CA8F44", [(31,35),(34,34),(37,35),(38,38),(36,40),(32,40),(30,38)])
        p.rect("#F8C062", 32, 35, 3, 2)
        p.rect("#3E4675", 34, 37, 1, 1)
    back = direction == "up"
    side = direction == "right"
    cap = [(8,18),(7,15),(10,10),(15,6),(21,4),(28,4),(34,7),(37,10),(41,12),
           (45,16),(48,20),(51,26),(51,30),(49,34),(45,36),(35,37),(22,36),
           (14,33),(11,29),(12,25),(16,20),(18,16),(17,13),(14,13),(11,15),(10,19)]
    if back:
        cap = [(61-x,y) for x,y in cap]
    p.shape("#7A336A", cap)
    def orient(points):
        return [(61-x if back else x,y) for x,y in points]
    p.fill("#561F4C", orient([(9,16),(12,12),(17,11),(22,12),(25,15),(23,18),
              (20,22),(18,26),(17,31),(21,34),(30,36),(22,35),(15,32),(13,28),
              (15,23),(19,17),(18,14),(15,13),(11,15),(10,18)]))
    p.line("#8E507C", orient([(13,10),(17,7),(22,6),(29,6),(34,8),(37,11)]), 2)
    p.fill("#67315E", orient([(25,20),(32,15),(40,13),(43,16),(36,17),(31,20),(27,23)]))
    if back:
        p.fill("#561F4C", [(25,16),(30,17),(33,21),(31,24),(26,25),(22,29),
                           (26,33),(35,34),(41,33),(39,35),(29,36),(20,34),(17,30),(18,25)])
        p.line("#8E507C", [(16,30),(22,33),(31,34),(39,33)])
    else:
        face_shape = ([(26,29),(29,25),(34,20),(39,17),(44,18),(47,22),(49,27),
                       (49,31),(46,34),(36,35),(28,33),(24,31)] if not side else
                      [(34,28),(36,24),(40,20),(45,17),(48,20),(50,26),(50,31),
                       (46,35),(38,35),(34,32)])
        p.fill("#B25A9B", face_shape)
        inset = ([(27,29),(31,25),(36,21),(40,19),(43,20),(46,23),(47,27),(47,31),
                  (44,33),(35,33),(29,32),(26,31)] if not side else
                 [(36,28),(39,24),(43,20),(46,19),(48,23),(49,27),(49,31),
                  (45,33),(38,33),(36,31)])
        p.fill(p.ink, inset)
        if "eyes" not in parts:
            eyes = (33,41) if not side else (40,46)
            if mood != "blink":
                for x in eyes:
                    p.rect("#E2ACFA", x, 27, 2, 1 if mood == "strained" else 2)
    p.rect("#CA8F44", 18 if not back else 42, 10, 2, 2)
    p.rect("#F8C062", 19 if not back else 41, 9, 1, 1)
    mitt(p, "geumeum", *SOCKETS["geumeum"][DIRECTIONS.index(direction)], direction == "right")


DRAW = dict(zip(FAMILIES, (kettle, lantern, book, frost, shadow)))


def draw_body(family, direction, scale=1, mood="neutral", hidden_parts=()):
    p = Painter(scale, INK[FAMILIES.index(family)])
    DRAW[family](p, direction, mood, set(hidden_parts))
    return p.image


def draw_selection(family):
    p = Painter(3, INK[FAMILIES.index(family)])
    DRAW[family](p, "down", "happy" if family in ("dalgu", "chorong") else "neutral", set())
    stage = {
        "dalgu": ("#3E3A44", "#A9A4AE", [(46,47),(48,35),(49,26),(55,26),(55,30)]),
        "chorong": ("#6B5218", "#EBC04A", [(45,48),(47,38),(49,27),(51,21),(54,20),(56,22),(56,25)]),
        "galpi": ("#5A3A2E", "#B88A5A", [(45,51),(49,38),(54,25),(54,21)]),
        "seonge": ("#314E83", "#9FDCFF", [(46,50),(49,37),(52,25),(55,17)]),
        "geumeum": ("#704456", "#A47384", [(45,54),(46,46),(49,39),(52,34),(53,26),(55,22)]),
    }[family]
    dark, light, points = stage
    p.line(p.ink, points, 3)
    p.line(dark, points, 2)
    p.line(light, [(x-.5,y) for x,y in points], .55)
    if family == "chorong":
        p.shape("#C89A22", [(53,25),(56,24),(59,26),(58,32),(53,32),(52,28)])
        p.fill("#FFF36B", [(54,26),(57,26),(57,31),(54,31)])
        p.line("#FFFFFF", [(55,27),(55,30)], .6)
    elif family == "galpi":
        p.shape("#EDE3C8", [(54,15),(58,19),(57,23),(54,25),(51,23),(52,19)])
        p.line("#8A5A3E", [(54,19),(54,23)], .7)
    elif family == "seonge":
        p.shape("#9FDCFF", [(55,11),(58,14),(57,21),(54,25),(52,21),(53,14)])
        p.fill("#F4FBFF", [(55,12),(55,19),(53,22),(54,15)])
        p.line("#E5C37B", [(51,23),(57,24)], 1.2)
    elif family == "geumeum":
        p.line("#704456", [(53,30),(55,27),(58,24),(59,22)], 1.5)
        p.shape("#8A5CFF", [(55,17),(58,21),(57,25),(55,27),(53,24),(53,20)])
        p.fill("#D9C7FF", [(55,18),(55,23),(54,24),(54,20)])
        p.line("#F8C062", [(51,33),(54,34)], 1.1)
    x, y = SOCKETS[family][0]
    mitt(p, family, x + 2, y, True)
    return p.image

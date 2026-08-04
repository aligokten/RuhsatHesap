const fs = require("fs");
const path = require("path");

const panelPath = path.join(__dirname, "..", "RFIX", "RuhsatHesapPanel.html");
const html = fs.readFileSync(panelPath, "utf8");
const scriptMatch = html.match(/<script>([\s\S]*)<\/script>/);

if (!scriptMatch) throw new Error("Panel script block is missing.");
new Function(scriptMatch[1]);

if (/\bprompt\s*\(/.test(scriptMatch[1]))
    throw new Error("Native browser prompt must not be used inside the Archicad palette.");

for (const requiredId of ["textDialog", "textDialogInput", "textDialogConfirm", "textDialogCancel"]) {
    if (!html.includes(`id="${requiredId}"`))
        throw new Error(`Panel text dialog element is missing: ${requiredId}`);
}

const comparatorSource = scriptMatch[1].match(/function naturalUnitCompare[\s\S]*?(?=\nfunction sortUnits)/);
if (!comparatorSource) throw new Error("Natural unit comparator is missing.");
eval(comparatorSource[0]);

const ordered = ["10", "7", "9", "8", "A10", "A2"]
    .map(number => ({number}))
    .sort(naturalUnitCompare)
    .map(unit => unit.number);
if (ordered.join(",") !== "7,8,9,10,A2,A10")
    throw new Error(`Unexpected independent-unit order: ${ordered.join(",")}`);

console.log("Ruhsat Hesap panel HTML tests passed.");

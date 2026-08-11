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

const tipCodeSource = scriptMatch[1].match(/function zoneTipCode[\s\S]*?(?=\nfunction sumMap)/);
if (!tipCodeSource) throw new Error("Dynamic construction TIP helper is missing.");
eval(tipCodeSource[0]);
if (zoneTipCode("Makina Dairesi + Denge Tankı") !== "MAKINA_DAIRESI_DENGE_TANKI")
    throw new Error("Dynamic construction TIP normalization failed.");

// Emsal Hesabı %30 columns must show the HESAP=EMSAL|TIP=... code that
// routes a zone into that column, mirroring the Yapı İnşaat Alanı hint above.
if (!scriptMatch[1].includes("HESAP=EMSAL|TIP="))
    throw new Error("Emsal Hesabı %30 dynamic TIP hint is missing.");

for (const requiredId of ["zoneDialog", "zoneForm", "zoneDialogPreview", "zoneDialogApply", "zoneDialogCopy", "zoneFormButton"]) {
    if (!html.includes(`id="${requiredId}"`))
        throw new Error(`Zone code form element is missing: ${requiredId}`);
}

// The zone form's pure helpers must build exactly the codes documented in
// ZON-ADLANDIRMA-STANDARDI.md. Tests/CoreTests.cpp parses these same literals
// through ParseZoneName, so the two layers stay in agreement.
const escSource = scriptMatch[1].match(/function esc\(v\)\{[\s\S]*?\n/);
if (!escSource) throw new Error("HTML escape helper is missing.");
const zoneSource = scriptMatch[1].match(/var zoneForm=\{[\s\S]*?(?=\nfunction openZoneDialog)/);
if (!zoneSource) throw new Error("Zone form helpers are missing.");

// Minimal DOM stand-in so renderZoneForm/updateZonePreview can run headless.
const zoneElements = {};
function zoneElement(id) {
    if (!zoneElements[id])
        zoneElements[id] = {id, innerHTML: "", textContent: "", disabled: false, classList: {toggle() {}}};
    return zoneElements[id];
}
global.document = {getElementById: zoneElement};
let state = null;
eval(escSource[0] + "\n" + zoneSource[0]);

function zoneFields(overrides) {
    return Object.assign(
        {block: "", unit: "", tip: "NET", customTip: "", hesapEmsal: false, rooms: "", mahal: "", nitelik: "", customNitelik: ""},
        overrides
    );
}
function buildWith(overrides) {
    zoneForm = zoneFields(overrides);
    return buildZoneName();
}

const zoneCases = [
    [{block: "a", unit: "01", tip: "NET", rooms: "3", mahal: "SALON", nitelik: "MESKEN"},
        "RH|BLOK=A|BB=01|TIP=NET|ODA=3|MAHAL=SALON|NITELIK=MESKEN"],
    // HESAP=EMSAL routes a floor-level area into the %30 istisna table.
    [{block: "A", tip: "MERDIVEN", hesapEmsal: true}, "RH|BLOK=A|HESAP=EMSAL|TIP=MERDIVEN"],
    // ...but it is meaningless for independent-unit areas and must be dropped.
    [{block: "A", unit: "01", tip: "NET", hesapEmsal: true}, "RH|BLOK=A|BB=01|TIP=NET"],
    // A free-text row name becomes a normalized TIP code.
    [{block: "A", tip: "__custom__", customTip: "Yangın Merdiveni"}, "RH|BLOK=A|TIP=YANGIN_MERDIVENI"],
    // A pipe typed into any value would break the code and must be stripped.
    [{block: "A", tip: "NET", mahal: "A|B"}, "RH|BLOK=A|TIP=NET|MAHAL=A B"],
    // Zero/blank room counts are omitted rather than written as ODA=0.
    [{block: "A", unit: "2", tip: "BRUT", rooms: "0"}, "RH|BLOK=A|BB=2|TIP=BRUT"],
    // BB/ODA/NITELIK only mean something on an independent-unit area, so a
    // floor-level TIP must drop them even when the fields still hold values.
    [{block: "A", unit: "01", tip: "MERDIVEN", rooms: "3", nitelik: "MESKEN"}, "RH|BLOK=A|TIP=MERDIVEN"],
    [{block: "A", unit: "01", tip: "ORTAK", rooms: "3", nitelik: "MESKEN", mahal: "ORTAK ALAN"},
        "RH|BLOK=A|TIP=ORTAK|MAHAL=ORTAK ALAN"]
];
for (const [fields, expected] of zoneCases) {
    const actual = buildWith(fields);
    if (actual !== expected) throw new Error(`Zone code mismatch: expected ${expected}, got ${actual}`);
}

const roundTrip = parseZoneName("RH|BLOK=A|HESAP=EMSAL|TIP=MERDIVEN|MAHAL=KAT HOLU");
if (!roundTrip || roundTrip.block !== "A" || roundTrip.tip !== "MERDIVEN" || roundTrip.hesap !== "EMSAL")
    throw new Error("Zone name round-trip parsing failed.");
if (parseZoneName("Salon") !== null)
    throw new Error("A non-RH zone name must not parse as a Ruhsat Hesap code.");

zoneForm = zoneFields({block: "", tip: "NET"});
if (!zoneFormIssue().error) throw new Error("A missing BLOK must block the apply action.");
zoneForm = zoneFields({block: "A", tip: "NET", unit: "01"});
if (zoneFormIssue().error) throw new Error("A complete unit code must be applicable.");

// The form itself must offer every field, hide the unit-only ones on floor-level
// TIPs, and surface the panel's own area rows as ready-made TIP options.
state = {
    blocks: [{name: "A"}, {name: "B"}],
    auxiliaryData: {
        constructionKeys: ["merdiven", "yangin_merdiveni"],
        thirtyPercentKeys: ["kat_holu"],
        zoneQualityOptions: ["DEPO"]
    }
};
zoneForm = zoneFields({tip: "NET", nitelik: "MESKEN"});
renderZoneForm();
for (const field of ["zoneFieldBlock", "zoneFieldUnit", "zoneFieldTip", "zoneFieldRooms", "zoneFieldMahal", "zoneFieldNitelik"]) {
    if (!zoneElements.zoneForm.innerHTML.includes(`id="${field}"`))
        throw new Error(`Zone form field is missing from the render: ${field}`);
}
for (const expected of ["YANGIN_MERDIVENI", "KAT_HOLU", ">DEPO<", 'value="A"']) {
    if (!zoneElements.zoneForm.innerHTML.includes(expected))
        throw new Error(`Zone form did not offer project data as a choice: ${expected}`);
}
if (zoneElements.zoneForm.innerHTML.includes("zoneFieldHesap"))
    throw new Error("The HESAP=EMSAL switch must stay hidden for independent-unit areas.");

zoneForm = zoneFields({tip: "MERDIVEN"});
renderZoneForm();
if (!zoneElements.zoneForm.innerHTML.includes("zoneFieldHesap"))
    throw new Error("The HESAP=EMSAL switch must appear for floor-level areas.");
for (const hidden of ["zoneFieldUnit", "zoneFieldRooms", "zoneFieldNitelik"]) {
    if (zoneElements.zoneForm.innerHTML.includes(`id="${hidden}"`))
        throw new Error(`Unit-only field must be hidden on a floor-level TIP: ${hidden}`);
}
if (!zoneElements.zoneDialogApply.disabled)
    throw new Error("Apply must stay disabled while BLOK is empty.");
zoneForm.block = "A";
zoneForm.hesapEmsal = true;
updateZonePreview();
if (zoneElements.zoneDialogPreview.textContent !== "RH|BLOK=A|HESAP=EMSAL|TIP=MERDIVEN")
    throw new Error(`Unexpected live preview: ${zoneElements.zoneDialogPreview.textContent}`);
if (zoneElements.zoneDialogApply.disabled)
    throw new Error("Apply must enable once the code is valid.");

// Reopening on an already-coded zone must round-trip it back into the fields.
if (!loadZoneFormFrom("RH|BLOK=C|BB=07|TIP=BALKON|ODA=2|MAHAL=BALKON|NITELIK=TICARI"))
    throw new Error("A valid existing zone code must load back into the form.");
if (zoneForm.block !== "C" || zoneForm.unit !== "07" || zoneForm.tip !== "BALKON" || zoneForm.nitelik !== "TICARI")
    throw new Error(`Zone form prefill mismatch: ${JSON.stringify(zoneForm)}`);
loadZoneFormFrom("RH|BLOK=D|TIP=HAVUZ KENARI");
if (zoneForm.tip !== "__custom__" || zoneElements.zoneDialogPreview.textContent !== "RH|BLOK=D|TIP=HAVUZ_KENARI")
    throw new Error("An unrecognized TIP must fall back to the free-text input.");

console.log("Ruhsat Hesap panel HTML tests passed.");

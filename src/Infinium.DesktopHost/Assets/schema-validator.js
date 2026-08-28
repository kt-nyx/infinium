import { rendererEnvelopeSchema, rendererLimits } from "./generated/renderer-contract.generated.js";
export function parseAndValidateRendererJson(text) {
    if (utf8ByteCount(text) > rendererLimits.maximum_message_bytes)
        throw new Error("The renderer message exceeds its byte bound.");
    const value = JSON.parse(text);
    validateRendererSchema(value);
    return value;
}
export function validateRendererSchema(value) {
    validateNode(value, rendererEnvelopeSchema, rendererEnvelopeSchema, "$");
}
function validateNode(value, schemaValue, rootValue, location) {
    if (typeof schemaValue === "boolean") {
        if (!schemaValue)
            fail(location, "is denied by schema");
        return;
    }
    const schema = object(schemaValue, "schema");
    const root = object(rootValue, "root schema");
    if (typeof schema.$ref === "string") {
        const name = schema.$ref.replace("#/$defs/", "");
        validateNode(value, object(root.$defs, "$defs")[name], root, location);
        if (name === "opaqueProductIdentity")
            validateOpaqueIdentity(value, location);
        if (name === "opaqueCursor")
            validateOpaqueCursor(value, location);
    }
    if (Array.isArray(schema.oneOf)) {
        const matches = schema.oneOf.filter((branch) => accepts(value, branch, root, location)).length;
        if (matches !== 1)
            fail(location, "does not satisfy exactly one schema branch");
    }
    if (Array.isArray(schema.allOf))
        for (const branch of schema.allOf)
            validateNode(value, branch, root, location);
    if (Array.isArray(schema.anyOf) && !schema.anyOf.some((branch) => accepts(value, branch, root, location)))
        fail(location, "does not satisfy a schema branch");
    if (schema.not !== undefined && accepts(value, schema.not, root, location))
        fail(location, "satisfies a denied schema branch");
    if (schema.if !== undefined)
        validateNode(value, accepts(value, schema.if, root, location) ? schema.then : schema.else, root, location);
    if (schema.const !== undefined && !equal(value, schema.const))
        fail(location, "does not equal its required constant");
    if (Array.isArray(schema.enum) && !schema.enum.some((item) => equal(value, item)))
        fail(location, "is outside the closed enum");
    if (schema.type === "string")
        validateString(value, schema, location);
    else if (schema.type === "integer" || schema.type === "number")
        validateNumber(value, schema, location, schema.type === "integer");
    else if (schema.type === "boolean" && typeof value !== "boolean")
        fail(location, "is not boolean");
    else if (schema.type === "array")
        validateArray(value, schema, root, location);
    else if (schema.type === "object" || schema.properties !== undefined || schema.required !== undefined || schema.additionalProperties !== undefined)
        validateObject(value, schema, root, location);
}
function validateObject(value, schema, root, location) {
    const instance = object(value, location);
    const properties = schema.properties === undefined ? {} : object(schema.properties, "properties");
    if (Array.isArray(schema.required))
        for (const name of schema.required)
            if (typeof name === "string" && !(name in instance))
                fail(location, `is missing ${name}`);
    if (schema.additionalProperties === false)
        for (const name of Object.keys(instance))
            if (!(name in properties))
                fail(location, `contains unknown field ${name}`);
    for (const [name, child] of Object.entries(properties))
        if (name in instance)
            validateNode(instance[name], child, root, `${location}.${name}`);
}
function validateArray(value, schema, root, location) {
    if (!Array.isArray(value))
        fail(location, "is not an array");
    const array = value;
    if (typeof schema.minItems === "number" && array.length < schema.minItems)
        fail(location, "has too few items");
    if (typeof schema.maxItems === "number" && array.length > schema.maxItems)
        fail(location, "has too many items");
    if (schema.uniqueItems === true && new Set(array.map((item) => JSON.stringify(item))).size !== array.length)
        fail(location, "contains duplicate items");
    if (schema.items !== undefined)
        array.forEach((item, index) => validateNode(item, schema.items, root, `${location}[${index}]`));
    if (Array.isArray(schema.prefixItems))
        schema.prefixItems.forEach((item, index) => { if (index < array.length)
            validateNode(array[index], item, root, `${location}[${index}]`); });
}
function validateString(value, schema, location) {
    if (typeof value !== "string")
        fail(location, "is not a string");
    if (typeof schema.minLength === "number" && value.length < schema.minLength)
        fail(location, "is too short");
    if (typeof schema.maxLength === "number" && value.length > schema.maxLength)
        fail(location, "is too long");
    if (typeof schema.pattern === "string" && !new RegExp(schema.pattern, "u").test(value))
        fail(location, "does not match its closed grammar");
}
function validateNumber(value, schema, location, integer) {
    if (typeof value !== "number" || !Number.isFinite(value) || (integer && !Number.isInteger(value)))
        fail(location, "is not a finite bounded number");
    if (typeof schema.minimum === "number" && value < schema.minimum)
        fail(location, "is below its minimum");
    if (typeof schema.maximum === "number" && value > schema.maximum)
        fail(location, "is above its maximum");
}
function accepts(value, schema, root, location) {
    try {
        validateNode(value, schema, root, location);
        return true;
    }
    catch {
        return false;
    }
}
function object(value, label) {
    if (typeof value !== "object" || value === null || Array.isArray(value))
        throw new Error(`The ${label} is not an object.`);
    return value;
}
function equal(left, right) { return JSON.stringify(left) === JSON.stringify(right); }
function fail(location, reason) { throw new Error(`Renderer schema validation failed at ${location}: ${reason}.`); }
function validateOpaqueIdentity(value, location) {
    if (typeof value !== "string" || utf8ByteCount(value) > 160 || [...value].some((symbol) => {
        const codePoint = symbol.codePointAt(0) ?? 0;
        return codePoint < 0x20 || codePoint === 0x7f || (codePoint >= 0xd800 && codePoint <= 0xdfff);
    }))
        fail(location, "is not a bounded opaque product identity");
}
function validateOpaqueCursor(value, location) {
    if (typeof value !== "string" || value.length % 4 === 1 || value.length > 10_923)
        fail(location, "is not a bounded canonical cursor");
    const last = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_".indexOf(value.at(-1) ?? "");
    if (last < 0 || (value.length % 4 === 2 && (last & 15) !== 0) || (value.length % 4 === 3 && (last & 3) !== 0))
        fail(location, "is not a canonical cursor");
}
function utf8ByteCount(value) {
    let count = 0;
    for (const symbol of value) {
        const codePoint = symbol.codePointAt(0) ?? 0;
        count += codePoint <= 0x7f ? 1 : codePoint <= 0x7ff ? 2 : codePoint <= 0xffff ? 3 : 4;
    }
    return count;
}

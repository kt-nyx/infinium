# RESEARCH-0055 — Slice 6 finite campaign and safety-identifier refresh

Status: Completed

Disposition: Retained owner-supplied official-document snapshot evidence for
the accepted M1/S6 finite three-operation campaign amendment only

Date: 2026-08-15

Last reviewed: 2026-08-15

## Source identities

The owner authorized only the bounded official OpenAI documentation refresh.
No API endpoint, provider request, credential, or billable operation was used.
The canonical logical URL and final URL were identical `.md` resources.

| Document | URL | Retrieved UTC | Bytes | SHA-256 | ETag | Last-Modified |
|---|---|---|---:|---|---|---|
| model | `https://developers.openai.com/api/docs/models/gpt-5.6-sol.md` | `2026-08-15T15:20:32.6761100Z` | 3,707 | `124cce0f52e97d87bca8d5c383dc9912bdfbcd8b5c3b54a7f209dc8383f9a4ad` | unavailable | unavailable |
| latest model | `https://developers.openai.com/api/docs/guides/latest-model.md` | `2026-08-15T15:20:32.7119604Z` | 18,668 | `7591e641abc3cb124b2173843a03d40ea05ee421c8a036f04dda44c79188953e` | `"2044a7bfb4dcefefffc5eafb03dafbb2"` | `Sat, 15 Aug 2026 02:04:31 GMT` |
| prompt caching | `https://developers.openai.com/api/docs/guides/prompt-caching.md` | `2026-08-15T15:20:32.7278778Z` | 27,997 | `2402d5a0bc2643daa28100121fa0397f1893d3e30552e9d0317ebf18288e8348` | `"bf8434ddbf443bfedc8e3e267927c41f"` | `Sat, 15 Aug 2026 03:24:41 GMT` |
| reasoning | `https://developers.openai.com/api/docs/guides/reasoning.md` | `2026-08-15T15:20:32.7568001Z` | 45,218 | `237067018b227133a45f5465b545fd06596631c6a96bd6adec5835450354d7b1` | `"be2d91e665fa23a8b183698ef3311ce6"` | `Sat, 15 Aug 2026 02:14:02 GMT` |
| structured outputs | `https://developers.openai.com/api/docs/guides/structured-outputs.md` | `2026-08-15T15:20:32.7878847Z` | 86,127 | `e894b773b2aa124f07baf3d3e232abf4cd8bed2e3d80f789078f98fed06b55db` | `"67add6991bc06417b289d2f5112b646a"` | `Sat, 15 Aug 2026 01:45:48 GMT` |
| safety best practices | `https://developers.openai.com/api/docs/guides/safety-best-practices.md` | `2026-08-15T15:20:32.8054571Z` | 7,626 | `109a4729274e9a27435f8f1f0dc9f70fdd0f83eec7766c49ea661af94879f403` | `"e3f5e02800ceb6962daaff2c93a6eb7f"` | `Sat, 15 Aug 2026 06:16:21 GMT` |

Every response had content type `text/markdown; charset=utf-8`. The predecessor
RESEARCH-0054 record remains separately bound at SHA-256
`bf585dee726ab386ca27570829e29ce51c3060a001e4a4749797357fd301c68a`.

## Confirmed external claims

- `gpt-5.6-sol` supports the Responses API and strict structured outputs.
- The standard short-context prices are $5 ordinary input, $0.50 cached input,
  $6.25 cache-write input, and $30 output per one million tokens.
- Inputs above 272,000 tokens use twice the input price and 1.5 times the
  output price. Every planned Slice 6 request remains below that threshold.
- Current guidance recommends a stable privacy-preserving `safety_identifier`
  for applications serving individual users.

## Accepted local resolution

Infinium generates one local cryptographically random 32-byte seed for the
product user. It transmits only lowercase-hex SHA-256 of the UTF-8 domain
`infinium.openai.safety-identifier/v1`, one NUL framing byte, and the raw seed.
The seed is never derived from credentials, provider/profile/account identity,
email, OS user or machine identity, source or prompt content, file/mod paths,
or advertising/telemetry identifiers. Missing or corrupt state fails closed;
after any possible provider start it can never be silently regenerated.

This resolution changes the provider request profile only. It does not change
the credential envelope, credential target, helper UX, account selection, or
billing scope, and it grants no live effect by itself.

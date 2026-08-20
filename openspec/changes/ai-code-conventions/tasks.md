## 1. PR template

- [ ] 1.1 Add `PULL_REQUEST_TEMPLATE.md` — summary, OpenSpec change reference,
  AI involvement (`AI: generated`/`AI: assisted`/`AI: none`), test evidence,
  quality-gate checklist, and the AI review checklist

## 2. Contribution guide

- [ ] 2.1 Add the AI-involvement convention + AI review checklist to
  `CONTRIBUTING.md` (markers, extra-care policy, non-blocking note)

## 3. Soft-warning workflow

- [ ] 3.1 Add `.github/workflows/ai-marker-check.yml` — PR opened/synchronize;
  compute added lines + marker count; comment if `added > 500 && markers == 0`
  (deduped); never fails

## 4. Verify

- [ ] 4.1 Simulate the diff/marker logic locally on a sample PR (500+ lines,
  no marker → comment would post; with marker → no comment; small diff → no
  comment)
- [ ] 4.2 Confirm the workflow exits 0 in all cases (non-blocking)

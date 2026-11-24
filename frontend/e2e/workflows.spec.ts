import { test, expect, type Page } from '@playwright/test';

async function login(page: Page) {
  await page.goto('/');
  await page.getByLabel('Workspace API key').fill(process.env.E2E_API_KEY!);
  await page.getByRole('button', { name: 'Open workspace', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Business overview', exact: true })).toBeVisible();
}

test('protected workspace rejects missing keys and accepts the configured key', async ({ page, request }) => {
  expect((await request.get('/api/state')).status()).toBe(401);
  await page.goto('/');
  await page.getByLabel('Workspace API key').fill('incorrect-test-key');
  await page.getByRole('button', { name: 'Open workspace', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('valid workspace API key');
  await page.getByLabel('Workspace API key').fill(process.env.E2E_API_KEY!);
  await page.getByRole('button', { name: 'Open workspace', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Business overview', exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByLabel('Workspace API key')).toBeVisible();
});

test('three domains produce real incidents and recovery needs evidence', async ({ page, request }) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await login(page);
  for (const name of ['Run commerce scenario', 'Run registration scenario', 'Run offboarding scenario']) {
    await page.getByRole('button', { name: /Demo lab/ }).click();
    await page.getByRole('button', { name, exact: true }).click();
    await expect(page.getByRole('status')).toContainText('synthetic journeys ingested');
  }
  const headers = { 'X-Api-Key': process.env.E2E_API_KEY! };
  const state = await (await request.get('/api/state', { headers })).json();
  expect(state.summary.totalEvaluations).toBe(9);
  expect(state.summary.openIncidents).toBe(3);
  expect(state.summary.satisfied).toBe(6);
  await page.getByRole('button', { name: /^Incidents/ }).click();
  await page.getByRole('button', { name: /^Every paid order gets fulfilled/ }).click();
  await page.getByRole('button', { name: 'Verify recovery', exact: true }).first().click();
  await expect(page.getByRole('status')).toContainText('Still missing');
  await page.getByLabel('Investigation note').fill('Checked the synthetic warehouse failure evidence.');
  await page.getByRole('button', { name: 'Acknowledge incident', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('acknowledged');
  await page.getByRole('button', { name: 'Simulate recovery', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Synthetic recovery evidence');
  await page.getByRole('button', { name: 'Verify recovery', exact: true }).first().click();
  await expect(page.getByRole('status')).toContainText('matching stored event evidence');
  const final = await (await request.get('/api/state', { headers })).json();
  expect(final.summary.resolvedIncidents).toBe(1);
  const incident = final.incidents.find((i: { appId: string }) => i.appId === 'demo-commerce');
  expect(incident.status).toBe('resolved');
  expect(incident.verification.evidenceEventIds).toHaveLength(2);
  expect(incident.timeline.some((e: { kind: string }) => e.kind === 'verification_failed')).toBe(true);
  expect(errors).toEqual([]);
});

test('operator can connect an application, author a rule, preview and ingest events', async ({ page, request }) => {
  await login(page);
  await page.getByRole('button', { name: 'Applications', exact: true }).click();
  await page.getByRole('button', { name: 'Connect application', exact: true }).click();
  await page.getByLabel('Application name', { exact: true }).fill('Browser test delivery');
  await page.getByLabel('Business domain', { exact: true }).fill('Logistics');
  await page.getByLabel('What does this application do?').fill('Synthetic browser-test workflow.');
  await page.getByRole('button', { name: 'Create application', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Browser test delivery', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Rule studio', exact: true }).click();
  await page.getByRole('button', { name: 'Create a rule', exact: true }).click();
  await page.getByLabel('Rule name', { exact: true }).fill('Dispatch creates tracking');
  await page.getByRole('combobox', { name: 'Application', exact: true }).selectOption({ label: 'Browser test delivery' });
  await page.getByLabel(/^Trigger event/).fill('parcel.dispatched');
  await page.getByLabel(/^Expected event types/).fill('tracking.created');
  await page.getByLabel(/^Time window/).fill('1');
  await page.getByRole('button', { name: /Preview/ }).click();
  await expect(page.getByText(/historical journeys checked/)).toBeVisible();
  await page.getByRole('button', { name: 'Activate rule', exact: true }).click();
  await expect(page.getByText('Dispatch creates tracking', { exact: true })).toBeVisible();
  const headers = { 'X-Api-Key': process.env.E2E_API_KEY! };
  const state = await (await request.get('/api/state', { headers })).json();
  const app = state.apps.find((a: { name: string }) => a.name === 'Browser test delivery');
  const event = { id: 'browser-trigger', appId: app.id, correlationId: 'parcel-browser-001', type: 'parcel.dispatched', occurredAt: new Date(Date.now() - 120_000).toISOString(), data: {} };
  await page.getByRole('button', { name: 'Event explorer', exact: true }).click();
  await page.getByRole('button', { name: 'Ingest events', exact: true }).click();
  await page.getByLabel('Event batch JSON').fill(JSON.stringify({ events: [event] }));
  await page.getByRole('button', { name: 'Ingest batch', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Event batch processed');
  const after = await (await request.get('/api/state', { headers })).json();
  expect(after.incidents.some((i: { ruleName: string; status: string }) => i.ruleName === 'Dispatch creates tracking' && i.status === 'open')).toBe(true);
});

test('mobile navigation keeps the workspace usable', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await login(page);
  await page.getByRole('button', { name: 'Open navigation', exact: true }).click();
  await page.getByRole('button', { name: /Demo lab/ }).click();
  await expect(page.getByRole('heading', { name: 'Demo lab', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Run commerce scenario', exact: true })).toBeVisible();
  const hasHorizontalOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
  expect(hasHorizontalOverflow).toBe(false);
});

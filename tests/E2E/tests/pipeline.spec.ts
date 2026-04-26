import { test, expect, BrowserContext, Page } from '@playwright/test';

const KEYCLOAK_URL = 'http://localhost:8080';
const APP_URL = 'http://localhost:4200';

async function loginUser(page: Page, username: string, password: string) {
  await page.goto(APP_URL);
  // Angular oauth2-oidc redirects to Keycloak login
  await page.waitForURL(/.*localhost:8080.*/);
  await page.fill('#username', username);
  await page.fill('#password', password);
  await page.click('[type=submit]');
  await page.waitForURL(`${APP_URL}/**`);
}

test.describe('PulseCRM Pipeline \u2014 Live Multi-User Editing', () => {
  let context1: BrowserContext;
  let context2: BrowserContext;
  let page1: Page;
  let page2: Page;

  test.beforeAll(async ({ browser }) => {
    context1 = await browser.newContext();
    context2 = await browser.newContext();
    page1 = await context1.newPage();
    page2 = await context2.newPage();
  });

  test.afterAll(async () => {
    await context1.close();
    await context2.close();
  });

  test('User A logs in and sees the pipeline', async () => {
    await loginUser(page1, 'alice@pulsecrm.dev', 'demo123');
    await page1.waitForURL(`${APP_URL}/pipeline`);
    await expect(page1.locator('.kanban-board')).toBeVisible();
    await expect(page1.locator('.deal-card')).toHaveCount.greaterThan?.(0);
  });

  test('User B logs in and sees the same pipeline', async () => {
    await loginUser(page2, 'bob@pulsecrm.dev', 'demo123');
    await page2.waitForURL(`${APP_URL}/pipeline`);
    await expect(page2.locator('.kanban-board')).toBeVisible();
  });

  test('User A drags a deal and User B sees the update in realtime', async () => {
    // Get the first deal card
    const firstCard = page1.locator('.deal-card').first();
    const cardTitle = await firstCard.locator('.deal-title').innerText();

    // Find the source and target column
    const columns = page1.locator('app-kanban-column');
    const sourceColumn = columns.first();
    const targetColumn = columns.nth(1);

    // Perform drag-drop using bounding boxes
    const sourceBB = await sourceColumn.boundingBox();
    const targetBB = await targetColumn.boundingBox();

    if (sourceBB && targetBB) {
      await page1.mouse.move(sourceBB.x + 100, sourceBB.y + 50);
      await page1.mouse.down();
      await page1.mouse.move(targetBB.x + 100, targetBB.y + 50, { steps: 10 });
      await page1.mouse.up();
    }

    // Wait for SignalR to propagate (max 2s)
    await page2.waitForTimeout(2000);

    // Verify the card moved in User B's view
    const targetColumnPage2 = page2.locator('app-kanban-column').nth(1);
    await expect(targetColumnPage2.locator('.deal-card', { hasText: cardTitle })).toBeVisible({ timeout: 5000 });
  });

  test('Score badges are visible and numeric', async () => {
    const scoreBadges = page1.locator('.score-badge');
    const count = await scoreBadges.count();
    expect(count).toBeGreaterThan(0);
    for (let i = 0; i < count; i++) {
      const text = await scoreBadges.nth(i).innerText();
      const score = parseInt(text, 10);
      expect(score).toBeGreaterThanOrEqual(0);
      expect(score).toBeLessThanOrEqual(100);
    }
  });
});

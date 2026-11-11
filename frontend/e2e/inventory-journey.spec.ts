import { mkdir } from 'node:fs/promises'
import { resolve } from 'node:path'
import { expect, test, type Page } from '@playwright/test'

const adminEmail = 'admin@inventorypro.e2e'
const adminPassword = 'E2E_Admin!2026_Secure'
const staffPassword = 'E2E_Staff!2026_Secure'
const categoryName = 'E2E Accessories'
const productName = 'E2E Wireless Scanner'
const productSku = 'E2E-SCAN-001'
const customerName = 'E2E Purchase Customer'
const customerEmail = 'e2e-customer@inventorypro.test'
const staffName = 'E2E Store Staff'
const staffEmail = 'e2e-staff@inventorypro.test'
const screenshotsDirectory = resolve(process.cwd(), '..', 'docs', 'screenshots')

test('completes inventory journey, captures page themes and checks Staff permissions', async ({ page }) => {
  await mkdir(screenshotsDirectory, { recursive: true })

  await signIn(page, adminEmail, adminPassword)

  await page.getByRole('link', { name: 'Categories' }).click()
  await page.getByRole('button', { name: 'Add category' }).click()
  const categoryDialog = page.getByRole('dialog', { name: 'Add category' })
  await expect(categoryDialog).toBeVisible()
  await categoryDialog.getByLabel('Name').fill(categoryName)
  await categoryDialog.getByLabel('Description').fill('Created by the Playwright end-to-end journey')
  await categoryDialog.getByRole('button', { name: 'Save category' }).click()
  await expect(page.getByRole('row').filter({ hasText: categoryName })).toBeVisible()

  await page.getByRole('link', { name: 'Products' }).click()
  await page.getByRole('button', { name: 'Add product' }).click()
  await page.getByLabel('Name', { exact: true }).fill(productName)
  await page.getByLabel('SKU').fill(productSku)
  await page.getByLabel('Category', { exact: true }).selectOption({ label: categoryName })
  await page.getByLabel('Price (₹)').fill('250')
  await page.getByLabel('Starting stock').fill('10')
  await page.getByLabel('Reorder level').fill('2')
  await page.getByRole('button', { name: 'Save', exact: true }).click()
  await expect(page.getByRole('row').filter({ hasText: productSku })).toBeVisible()

  await page.getByRole('link', { name: 'Customers' }).click()
  await page.getByRole('button', { name: 'Add customer' }).click()
  await page.getByLabel('Full name', { exact: true }).fill(customerName)
  await page.getByLabel('Email', { exact: true }).fill(customerEmail)
  await page.getByRole('button', { name: 'Save customer' }).click()
  await expect(page.getByRole('row').filter({ hasText: customerEmail })).toBeVisible()

  await page.getByRole('link', { name: 'Orders' }).click()
  await page.getByRole('button', { name: 'New order' }).click()
  await page.getByLabel('Customer', { exact: true }).selectOption({ label: `${customerName} · ${customerEmail}` })
  await page.getByLabel('Add products').fill(productName)
  await page.getByRole('button', { name: new RegExp(productName) }).click()
  await page.getByLabel(`Quantity of ${productName}`).fill('8')
  await page.getByRole('button', { name: 'Place order' }).click()

  const orderRow = page.getByRole('row').filter({ hasText: customerName })
  await expect(orderRow).toBeVisible()

  await page.getByRole('link', { name: 'Reorder planner' }).click()
  await expect(page.getByRole('row').filter({ hasText: productSku })).toBeVisible()

  await page.getByRole('link', { name: 'Orders' }).click()
  const statusControl = orderRow.getByRole('combobox', { name: /Change status of/ })
  await statusControl.selectOption({ label: 'Cancelled' })
  await page.getByRole('dialog', { name: 'Cancel order' }).getByRole('button', { name: 'Cancel order' }).click()
  await expect(orderRow.getByText('Cancelled')).toBeVisible()
  await orderRow.getByRole('button', { name: /^View / }).click()
  await expect(page.getByRole('status').getByText('Stock from this order has been restored to inventory.')).toBeVisible()
  await page.getByRole('button', { name: 'Close order details' }).click()

  await page.getByRole('link', { name: 'Products' }).click()
  const productRow = page.getByRole('row').filter({ hasText: productSku })
  await expect(productRow.getByRole('cell').nth(3)).toHaveText('10')

  await page.getByRole('link', { name: 'Team' }).click()
  await page.getByRole('button', { name: 'Add member' }).first().click()
  await page.getByLabel('Full name', { exact: true }).fill(staffName)
  await page.getByLabel('Email address').fill(staffEmail)
  await page.getByLabel('Temporary password').fill(staffPassword)
  await page.getByRole('dialog', { name: 'Add team member' }).getByRole('button', { name: 'Add member', exact: true }).click()
  await expect(page.getByRole('row').filter({ hasText: staffEmail })).toBeVisible()

  await captureScreenshots(page)

  await page.getByRole('button', { name: 'Sign out' }).click()
  await expect(page).toHaveURL(/\/login$/)
  await signIn(page, staffEmail, staffPassword, '/orders')
  await expect(page.getByRole('link', { name: 'Team' })).toHaveCount(0)

  await page.goto('/team')
  await expect(page).toHaveURL('/')
  await page.getByRole('link', { name: 'Products' }).click()
  await expect(page.getByRole('button', { name: 'Add product' })).toHaveCount(0)
  await page.getByRole('link', { name: 'Categories' }).click()
  await expect(page.getByRole('button', { name: 'Add category' })).toHaveCount(0)
})

async function signIn(page: Page, email: string, password: string, expectedPath = '/') {
  await page.goto('/login')
  await page.getByLabel('Email', { exact: true }).fill(email)
  await page.getByLabel('Password', { exact: true }).fill(password)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(expectedPath)
  await expect(page.getByRole('heading', { name: expectedPath === '/' ? 'Dashboard' : 'Orders' })).toBeVisible()
}

async function captureScreenshots(page: Page) {
  const screens = [
    { name: 'dashboard', path: '/', ready: page.getByRole('heading', { name: 'Dashboard' }) },
    { name: 'planner', path: '/reorder', ready: page.getByRole('heading', { name: 'Reorder planner' }) },
    { name: 'products', path: '/products', ready: page.getByRole('row').filter({ hasText: productSku }) },
    { name: 'orders', path: '/orders', ready: page.getByRole('row').filter({ hasText: customerName }) },
  ]

  await page.evaluate(() => localStorage.removeItem('inventorypro-theme'))
  await page.reload()
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

  for (const screen of screens) {
    await page.setViewportSize({ width: 1440, height: 1000 })
    await page.goto(screen.path)
    await expect(screen.ready).toBeVisible()
    await page.screenshot({ path: resolve(screenshotsDirectory, `${screen.name}-desktop.png`), fullPage: true })

    await page.setViewportSize({ width: 390, height: 844 })
    await expect(screen.ready).toBeVisible()
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390)
    await page.screenshot({ path: resolve(screenshotsDirectory, `${screen.name}-mobile.png`), fullPage: true })
  }

  await page.setViewportSize({ width: 1440, height: 1000 })
  await page.getByRole('button', { name: 'Switch to dark mode' }).first().click()
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
  for (const screen of screens) {
    await page.goto(screen.path)
    await expect(screen.ready).toBeVisible()
    await page.screenshot({ path: resolve(screenshotsDirectory, `${screen.name}-dark.png`), fullPage: true })
  }
}

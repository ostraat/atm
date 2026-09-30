<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import ActionBar from '../components/ActionBar.vue'
import { getAccounts } from '../api/client'
import { setHeaderBack } from '../headerBack'
import type { Account } from '../types/api'
import { formatMoney } from '../format'

const accounts = ref<Account[]>([])
const error = ref('')
const loading = ref(true)

onMounted(async () => {
  setHeaderBack(null)
  try {
    const response = await getAccounts()
    accounts.value = response.accounts
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load accounts'
  } finally {
    loading.value = false
  }
})

onUnmounted(() => setHeaderBack(null))
</script>

<template>
  <ActionBar />
  <p v-if="loading" class="muted">Loading…</p>
  <p v-else-if="error" class="error">{{ error }}</p>
  <div v-else class="account-list">
    <router-link
      v-for="account in accounts"
      :key="account.id"
      class="card account-row"
      :to="{ name: 'account', params: { id: account.id } }"
    >
      <div class="label">{{ account.label }}</div>
      <div class="balance">{{ formatMoney(account.balance) }}</div>
      <div v-if="account.latestTransaction" class="muted latest">
        {{ account.latestTransaction.label }} · {{ formatMoney(account.latestTransaction.amount) }}
      </div>
    </router-link>
  </div>
</template>

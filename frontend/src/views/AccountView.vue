<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import ActionBar from '../components/ActionBar.vue'
import { getAccount, getTransactions } from '../api/client'
import type { Account, Transaction } from '../types/api'
import { formatMoney } from '../format'

const props = defineProps<{ id: string }>()
const router = useRouter()
const account = ref<Account | null>(null)
const transactions = ref<Transaction[]>([])
const error = ref('')
const loading = ref(true)

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [accountResponse, txResponse] = await Promise.all([
      getAccount(props.id),
      getTransactions(props.id)
    ])
    account.value = accountResponse.account
    transactions.value = txResponse.transactions
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load account'
  } finally {
    loading.value = false
  }
}

onMounted(load)
watch(() => props.id, load)
</script>

<template>
  <button class="secondary" style="margin-bottom: 1rem" @click="router.push({ name: 'home' })">Back</button>
  <ActionBar :account-id="id" />
  <p v-if="loading" class="muted">Loading…</p>
  <p v-else-if="error" class="error">{{ error }}</p>
  <template v-else-if="account">
    <div class="card" style="margin-bottom: 1rem">
      <div class="label">{{ account.label }}</div>
      <div class="balance">{{ formatMoney(account.balance) }}</div>
    </div>
    <h2>Transactions</h2>
    <ul class="tx-list card">
      <li v-for="(tx, index) in transactions" :key="index">
        <div>
          <div>{{ tx.label }}</div>
          <div class="muted">{{ new Date(tx.createdAt).toLocaleString() }}</div>
        </div>
        <strong>{{ formatMoney(tx.amount) }}</strong>
      </li>
      <li v-if="transactions.length === 0" class="muted">No transactions yet.</li>
    </ul>
  </template>
</template>

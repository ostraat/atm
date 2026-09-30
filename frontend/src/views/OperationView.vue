<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { deposit, getAccounts, transfer, withdraw } from '../api/client'
import { setHeaderBack } from '../headerBack'
import type { Account } from '../types/api'
import { formatMoney, parseDollarsToCents } from '../format'

type Step = 'amount' | 'account' | 'from' | 'to' | 'summary'

const props = defineProps<{ type: string }>()
const route = useRoute()
const router = useRouter()

const step = ref<Step>('amount')
const amountText = ref('')
const accounts = ref<Account[]>([])
const selectedAccountId = ref<string | null>(null)
const fromId = ref<string | null>(null)
const toId = ref<string | null>(null)
const error = ref('')
const submitting = ref(false)

const isTransfer = computed(() => props.type === 'transfer')
const title = computed(() => {
  if (props.type === 'deposit') return 'Deposit'
  if (props.type === 'withdraw') return 'Withdraw'
  return 'Transfer'
})

const amountCents = computed(() => parseDollarsToCents(amountText.value))
const selectedAccount = computed(() => accounts.value.find(a => a.id === selectedAccountId.value) ?? null)
const fromAccount = computed(() => accounts.value.find(a => a.id === fromId.value) ?? null)
const toAccount = computed(() => accounts.value.find(a => a.id === toId.value) ?? null)
const sameAccount = computed(() => !!fromId.value && fromId.value === toId.value)

const amountRecap = computed(() =>
  amountCents.value == null ? null : `Amount: ${formatMoney(amountCents.value)}`
)

const accountRecap = computed(() => {
  if (isTransfer.value) return null
  if (!selectedAccount.value) return null
  const prep = props.type === 'deposit' ? 'To' : 'From'
  return `${prep} account: ${selectedAccount.value.label}`
})

const fromRecap = computed(() =>
  fromAccount.value ? `From account: ${fromAccount.value.label}` : null
)

const toRecap = computed(() =>
  toAccount.value ? `To account: ${toAccount.value.label}` : null
)

function goBackStep() {
  error.value = ''
  if (step.value === 'summary') {
    step.value = isTransfer.value ? 'to' : 'account'
    // Deposit/withdraw with only amount→summary when account prefilled:
    // still go to account step so user can change it, preserving selection.
    return
  }
  if (step.value === 'to') {
    step.value = 'from'
    return
  }
  if (step.value === 'from' || step.value === 'account') {
    step.value = 'amount'
    return
  }
  router.back()
}

onMounted(async () => {
  setHeaderBack(goBackStep)
  const response = await getAccounts()
  accounts.value = response.accounts
  const presetAccount = typeof route.query.accountId === 'string' ? route.query.accountId : null
  const presetFrom = typeof route.query.from === 'string' ? route.query.from : null
  if (!isTransfer.value && presetAccount) {
    selectedAccountId.value = presetAccount
  }
  if (isTransfer.value && presetFrom) {
    fromId.value = presetFrom
  }
})

onUnmounted(() => setHeaderBack(null))

watch(() => props.type, () => {
  step.value = 'amount'
  error.value = ''
})

watch(step, () => setHeaderBack(goBackStep))

function goNextFromAmount() {
  error.value = ''
  if (amountCents.value == null) {
    error.value = 'Enter a valid amount greater than zero (e.g. 12.50).'
    return
  }
  if (isTransfer.value) {
    // Always include both from and to steps (even when from is prefilled).
    step.value = 'from'
  } else {
    step.value = 'account'
  }
}

function chooseAccount(id: string) {
  selectedAccountId.value = id
  step.value = 'summary'
}

function chooseFrom(id: string) {
  fromId.value = id
  step.value = 'to'
}

function chooseTo(id: string) {
  toId.value = id
  step.value = 'summary'
}

const summaryText = computed(() => {
  const amount = amountCents.value
  if (amount == null) return ''
  if (isTransfer.value && fromAccount.value && toAccount.value) {
    return `Transfer ${formatMoney(amount)} from ${fromAccount.value.label} to ${toAccount.value.label}.`
  }
  if (selectedAccount.value) {
    return `${title.value} ${formatMoney(amount)} ${props.type === 'deposit' ? 'to' : 'from'} ${selectedAccount.value.label}.`
  }
  return ''
})

async function confirm() {
  error.value = ''
  const amount = amountCents.value
  if (amount == null) return
  if (isTransfer.value) {
    if (!fromId.value || !toId.value || sameAccount.value) return
  } else if (!selectedAccountId.value) {
    return
  }

  submitting.value = true
  try {
    if (props.type === 'deposit') {
      await deposit(selectedAccountId.value!, amount)
    } else if (props.type === 'withdraw') {
      await withdraw(selectedAccountId.value!, amount)
    } else {
      await transfer(fromId.value!, toId.value!, amount)
    }
    if (selectedAccountId.value) {
      await router.push({ name: 'account', params: { id: selectedAccountId.value } })
    } else if (fromId.value) {
      await router.push({ name: 'account', params: { id: fromId.value } })
    } else {
      await router.push({ name: 'home' })
    }
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Request failed'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <h2>{{ title }}</h2>

  <div v-if="step === 'amount'" class="card">
    <label for="amount">Amount (dollars)</label>
    <input id="amount" v-model="amountText" inputmode="decimal" placeholder="25.00" />
    <p v-if="error" class="error">{{ error }}</p>
    <div class="wizard-nav">
      <button type="button" @click="goNextFromAmount">Continue</button>
    </div>
  </div>

  <div v-else-if="step === 'account'" class="card">
    <div v-if="amountRecap" class="step-recap">
      <p>{{ amountRecap }}</p>
    </div>
    <p>Select account</p>
    <button
      v-for="account in accounts"
      :key="account.id"
      type="button"
      class="choice"
      :class="{ selected: selectedAccountId === account.id }"
      @click="chooseAccount(account.id)"
    >
      {{ account.label }} · {{ formatMoney(account.balance) }}
    </button>
  </div>

  <div v-else-if="step === 'from'" class="card">
    <div v-if="amountRecap" class="step-recap">
      <p>{{ amountRecap }}</p>
    </div>
    <p>Transfer from</p>
    <button
      v-for="account in accounts"
      :key="account.id"
      type="button"
      class="choice"
      :class="{ selected: fromId === account.id }"
      @click="chooseFrom(account.id)"
    >
      {{ account.label }} · {{ formatMoney(account.balance) }}
    </button>
  </div>

  <div v-else-if="step === 'to'" class="card">
    <div class="step-recap">
      <p v-if="amountRecap">{{ amountRecap }}</p>
      <p v-if="fromRecap">{{ fromRecap }}</p>
    </div>
    <p>Transfer to</p>
    <button
      v-for="account in accounts"
      :key="account.id"
      type="button"
      class="choice"
      :class="{ selected: toId === account.id }"
      @click="chooseTo(account.id)"
    >
      {{ account.label }} · {{ formatMoney(account.balance) }}
    </button>
  </div>

  <div v-else class="card">
    <div class="step-recap">
      <p v-if="amountRecap">{{ amountRecap }}</p>
      <p v-if="accountRecap">{{ accountRecap }}</p>
      <p v-if="fromRecap">{{ fromRecap }}</p>
      <p v-if="toRecap">{{ toRecap }}</p>
    </div>
    <p>{{ summaryText }}</p>
    <p v-if="sameAccount" class="warning">From and to must be different accounts.</p>
    <p v-if="error" class="error">{{ error }}</p>
    <div class="wizard-nav">
      <button
        type="button"
        :disabled="submitting || sameAccount || (isTransfer ? !(fromId && toId) : !selectedAccountId)"
        @click="confirm"
      >Confirm</button>
    </div>
  </div>
</template>

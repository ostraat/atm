<script setup lang="ts">
defineProps<{
  accountId?: string
}>()
</script>

<template>
  <div class="actions">
    <router-link
      class="btn"
      :to="{ name: 'operation', params: { type: 'deposit' }, query: accountId ? { accountId } : {} }"
    >Deposit</router-link>
    <router-link
      class="btn"
      :to="{ name: 'operation', params: { type: 'withdraw' }, query: accountId ? { accountId } : {} }"
    >Withdraw</router-link>
    <router-link
      class="btn"
      :to="{ name: 'operation', params: { type: 'transfer' }, query: accountId ? { from: accountId } : {} }"
    >Transfer</router-link>
  </div>
</template>

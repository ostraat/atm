import { createRouter, createWebHistory } from 'vue-router'
import HomeView from './views/HomeView.vue'
import AccountView from './views/AccountView.vue'
import OperationView from './views/OperationView.vue'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'home', component: HomeView },
    { path: '/accounts/:id', name: 'account', component: AccountView, props: true },
    {
      path: '/op/:type',
      name: 'operation',
      component: OperationView,
      props: true
    }
  ]
})

import { client } from './client'
import type { Distillery } from '../types'

export const getDistilleries = (category?: string) =>
  client.get<Distillery[]>('/distilleries', {
    params: category ? { category } : undefined,
  }).then(r => r.data)

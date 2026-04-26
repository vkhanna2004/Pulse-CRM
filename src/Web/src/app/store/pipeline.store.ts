import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';

export interface Deal {
  id: string;
  tenantId: string;
  title: string;
  value: number;
  currency: string;
  stageId: string;
  positionInStage: number;
  ownerId: string;
  ownerDisplayName: string;
  contactId?: string;
  contactDisplayName?: string;
  score: number;
  isClosed: boolean;
  expectedCloseDate?: string;
  rowVersion: number;
  createdAt: string;
  updatedAt: string;
}

export interface Stage {
  id: string;
  name: string;
  order: number;
  isTerminal: boolean;
  deals: Deal[];
}

export interface Pipeline {
  id: string;
  name: string;
  stages: Stage[];
}

export interface PipelineState {
  pipeline: Pipeline | null;
  presence: Record<string, string[]>;
  loading: boolean;
  error: string | null;
}

export const PipelineStore = signalStore(
  { providedIn: 'root' },
  withState<PipelineState>({
    pipeline: null,
    presence: {},
    loading: false,
    error: null
  }),
  withMethods((store) => ({
    setPipeline(pipeline: Pipeline): void {
      patchState(store, { pipeline, loading: false, error: null });
    },

    setLoading(loading: boolean): void {
      patchState(store, { loading });
    },

    setError(error: string | null): void {
      patchState(store, { error, loading: false });
    },

    applyDealCreated(event: Deal): void {
      const pipeline = store.pipeline();
      if (!pipeline) return;
      const stages = pipeline.stages.map(s => {
        if (s.id !== event.stageId) return s;
        const alreadyExists = s.deals.some(d => d.id === event.id);
        if (alreadyExists) return s;
        return {
          ...s,
          deals: [...s.deals, event].sort((a, b) => a.positionInStage - b.positionInStage)
        };
      });
      patchState(store, { pipeline: { ...pipeline, stages } });
    },

    applyDealUpdated(event: Partial<Deal> & { id: string }): void {
      const pipeline = store.pipeline();
      if (!pipeline) return;
      const stages = pipeline.stages.map(s => ({
        ...s,
        deals: s.deals.map(d => d.id === event.id ? { ...d, ...event } : d)
      }));
      patchState(store, { pipeline: { ...pipeline, stages } });
    },

    applyDealMoved(event: {
      dealId: string;
      fromStageId: string;
      toStageId: string;
      positionInStage: number;
      rowVersion: number;
    }): void {
      const pipeline = store.pipeline();
      if (!pipeline) return;

      const deal = pipeline.stages.flatMap(s => s.deals).find(d => d.id === event.dealId);
      if (!deal) return;

      const updatedDeal: Deal = {
        ...deal,
        stageId: event.toStageId,
        positionInStage: event.positionInStage,
        rowVersion: event.rowVersion
      };

      const isSameStage = event.fromStageId === event.toStageId;

      const stages = pipeline.stages.map(s => {
        if (isSameStage && s.id === event.fromStageId) {
          // Reorder within same column
          return {
            ...s,
            deals: s.deals
              .map(d => d.id === event.dealId ? updatedDeal : d)
              .sort((a, b) => a.positionInStage - b.positionInStage)
          };
        }
        if (!isSameStage && s.id === event.fromStageId) {
          return { ...s, deals: s.deals.filter(d => d.id !== event.dealId) };
        }
        if (!isSameStage && s.id === event.toStageId) {
          const existing = s.deals.some(d => d.id === event.dealId);
          const baseDeals = existing
            ? s.deals.map(d => d.id === event.dealId ? updatedDeal : d)
            : [...s.deals, updatedDeal];
          return { ...s, deals: baseDeals.sort((a, b) => a.positionInStage - b.positionInStage) };
        }
        return s;
      });

      patchState(store, { pipeline: { ...pipeline, stages } });
    },

    applyScoreUpdated(event: { dealId: string; score: number; factors?: Record<string, number> }): void {
      const pipeline = store.pipeline();
      if (!pipeline) return;
      const stages = pipeline.stages.map(s => ({
        ...s,
        deals: s.deals.map(d => d.id === event.dealId ? { ...d, score: event.score } : d)
      }));
      patchState(store, { pipeline: { ...pipeline, stages } });
    },

    applyPresenceChanged(event: { userId: string; deals: string[] }): void {
      const presence = { ...store.presence(), [event.userId]: event.deals };
      patchState(store, { presence });
    },

    serverApplyDealMove(deal: Deal): void {
      const pipeline = store.pipeline();
      if (!pipeline) return;
      const stages = pipeline.stages.map(s => ({
        ...s,
        deals: s.deals.map(d => d.id === deal.id ? deal : d)
      }));
      patchState(store, { pipeline: { ...pipeline, stages } });
    },

    optimisticallyMoveDeal(
      dealId: string,
      fromStageId: string,
      toStageId: string,
      positionInStage: number
    ): void {
      const pipeline = store.pipeline();
      if (!pipeline) return;

      const deal = pipeline.stages.flatMap(s => s.deals).find(d => d.id === dealId);
      if (!deal) return;

      const movedDeal: Deal = { ...deal, stageId: toStageId, positionInStage };
      const isSameStage = fromStageId === toStageId;

      const stages = pipeline.stages.map(s => {
        if (isSameStage && s.id === fromStageId) {
          return {
            ...s,
            deals: s.deals
              .map(d => d.id === dealId ? movedDeal : d)
              .sort((a, b) => a.positionInStage - b.positionInStage)
          };
        }
        if (!isSameStage && s.id === fromStageId) {
          return { ...s, deals: s.deals.filter(d => d.id !== dealId) };
        }
        if (!isSameStage && s.id === toStageId) {
          return {
            ...s,
            deals: [...s.deals, movedDeal].sort((a, b) => a.positionInStage - b.positionInStage)
          };
        }
        return s;
      });

      patchState(store, { pipeline: { ...pipeline, stages } });
    }
  }))
);
